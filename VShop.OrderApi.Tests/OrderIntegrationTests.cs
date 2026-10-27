using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using VShop.OrderApi.Context;
using VShop.OrderApi.Controllers;
using VShop.OrderApi.Models;
using VShop.OrderApi.Repositories;
using VShop.OrderApi.Services;

internal static class OrderIntegrationTests
{
    public static async Task Run(string connection)
    {
        var name = "VShopOrderTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false };
        var test = new SqlConnectionStringBuilder(connection) { InitialCatalog = name, Pooling = false };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(test.ConnectionString).Options;
        var created = false;
        WebApplication? app = null;
        try
        {
            await using (var sql = new SqlConnection(master.ConnectionString))
            {
                await sql.OpenAsync();
                await using var command = sql.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{name}]";
                await command.ExecuteNonQueryAsync();
                created = true;
            }
            await using (var db = new AppDbContext(options)) await db.Database.MigrateAsync();
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("chave-local-de-teste-exclusiva-com-mais-de-32-bytes"));
            var builder = WebApplication.CreateBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Critical);
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(test.ConnectionString));
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<OrderPreparationService>();
            builder.Services.AddScoped<CheckoutSagaService>();
            builder.Services.AddScoped<SagaRemoteClient>();
            builder.Services.AddScoped<IPaymentService, SimulatedPaymentService>();
            builder.Services.AddHttpClient("CartApi", c => c.BaseAddress = new Uri("http://cart.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new FakeCartHandler());
            builder.Services.AddControllers().AddApplicationPart(typeof(OrderController).Assembly);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey
                };
            });
            builder.Services.AddAuthorization(o => o.AddPolicy("ApiScope", p => p.RequireAuthenticatedUser().RequireClaim("scope", "Vshop.OrderApi")));
            app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            string Token(string user, string scope) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("sub", user), new Claim("scope", scope) }, expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)));
            object Payload(string key, string firstName = "Ana") => new { firstName, lastName = "Silva", email = "ana@example.com", telephone = "11999999999", address = "Rua 1", idempotencyKey = key };
            async Task<HttpResponseMessage> Send(string? token, object payload)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/order/prepare") { Content = JsonContent.Create(payload) };
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await client.SendAsync(request);
            }
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            var passed = 0;
            async Task Test(string label, Func<Task> testCase) { await testCase(); passed++; Console.WriteLine($"PASSOU: {label}"); }
            await Test("sem autenticação", async () => { using var r = await Send(null, Payload("noauth")); Check(r.StatusCode == HttpStatusCode.Unauthorized, $"Recebido {r.StatusCode}"); });
            await Test("sem escopo da OrderApi", async () => { using var r = await Send(Token("user-a", "Vshop.CartApi"), Payload("noscope")); Check(r.StatusCode == HttpStatusCode.Forbidden, $"Recebido {r.StatusCode}"); });
            var token = Token("user-a", "Vshop.OrderApi");
            await Test("criação persistente com valores do servidor", async () =>
            {
                using var r = await Send(token, Payload("basic")); Check(r.StatusCode == HttpStatusCode.Created, $"Recebido {r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
                await using var db = new AppDbContext(options);
                var order = await db.OrderHeaders.Include(o => o.OrderDetails).SingleAsync(o => o.IdempotencyKey == "basic");
                Check(order.UserId == "user-a" && order.Status == OrderStatus.Pending && order.PaymentStatus == PaymentStatus.Pending &&
                    order.SubtotalAmount == 75m && order.TotalAmount == 75m && order.DiscountAmount == 0 && order.Address == "Rua 1" &&
                    order.OrderDetails.Single().Quantity == 3 && order.OrderDetails.Single().UnitPrice == 25m, "Pedido persistido incorretamente.");
            });
            await Test("repetição idempotente", async () => { using var r = await Send(token, Payload("basic")); Check(r.StatusCode == HttpStatusCode.OK, $"Recebido {r.StatusCode}"); });
            await Test("mesma chave com dados diferentes retorna conflito", async () => { using var r = await Send(token, Payload("basic", "Outra")); Check(r.StatusCode == HttpStatusCode.Conflict, $"Recebido {r.StatusCode}"); });
            await Test("mesma chave com preço alterado retorna conflito", async () =>
            {
                FakeCartHandler.Price = 26m;
                try { using var r = await Send(token, Payload("basic")); Check(r.StatusCode == HttpStatusCode.Conflict, $"Recebido {r.StatusCode}"); }
                finally { FakeCartHandler.Price = 25m; }
            });
            await Test("requisições simultâneas com mesma chave", async () =>
            {
                var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Send(token, Payload("concurrent"))));
                try { Check(results.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && results.Count(r => r.StatusCode == HttpStatusCode.OK) == 5,
                    $"Respostas: {string.Join(',', results.Select(r => (int)r.StatusCode))}"); }
                finally { foreach (var r in results) r.Dispose(); }
                await using var db = new AppDbContext(options);
                Check(await db.OrderHeaders.CountAsync(o => o.UserId == "user-a" && o.IdempotencyKey == "concurrent") == 1, "Pedidos duplicados.");
            });
            await Test("requisições simultâneas com dados diferentes não sobrescrevem", async () =>
            {
                var results = await Task.WhenAll(Send(token, Payload("mixed", "Ana")), Send(token, Payload("mixed", "Bia")));
                try { Check(results.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && results.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
                    $"Respostas: {string.Join(',', results.Select(r => (int)r.StatusCode))}"); }
                finally { foreach (var r in results) r.Dispose(); }
            });
            await Test("chave idêntica pode ser usada por outro usuário", async () => { using var r = await Send(Token("user-b", "Vshop.OrderApi"), Payload("basic")); Check(r.StatusCode == HttpStatusCode.Created, $"Recebido {r.StatusCode}"); });
            await Test("outro usuário não pode consultar o pedido", async () =>
            {
                long id;
                await using (var db = new AppDbContext(options)) id = await db.OrderHeaders.Where(o => o.UserId == "user-a" && o.IdempotencyKey == "basic").Select(o => o.OrderHeaderId).SingleAsync();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{id}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("user-b", "Vshop.OrderApi"));
                using var r = await client.SendAsync(request);
                Check(r.StatusCode == HttpStatusCode.NotFound, $"Recebido {r.StatusCode}");
            });
            await Test("identidade com caixa diferente não consulta pedido", async () =>
            {
                long id;
                await using (var db = new AppDbContext(options)) id = await db.OrderHeaders.Where(o => o.UserId == "user-a" && o.IdempotencyKey == "basic").Select(o => o.OrderHeaderId).SingleAsync();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{id}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("USER-A", "Vshop.OrderApi"));
                using var r = await client.SendAsync(request);
                Check(r.StatusCode == HttpStatusCode.NotFound, $"Recebido {r.StatusCode}");
            });
            await Test("identidades distintas por caixa podem usar a mesma chave", async () =>
            {
                using var r = await Send(Token("USER-A", "Vshop.OrderApi"), Payload("basic"));
                Check(r.StatusCode == HttpStatusCode.Created, $"Recebido {r.StatusCode}");
            });
            await Test("identidade e preços arbitrários são rejeitados", async () =>
            {
                using var r = await Send(token, new { firstName = "Ana", lastName = "Silva", email = "ana@example.com", telephone = "11999999999", idempotencyKey = "forged", userId = "user-b", subtotalAmount = 0m });
                Check(r.StatusCode == HttpStatusCode.BadRequest, $"Recebido {r.StatusCode}");
            });
            Console.WriteLine($"RESULTADO integração HTTP/SQL: {passed} passaram; 0 falharam.");
        }
        finally
        {
            if (app != null) await app.DisposeAsync();
            if (created) { await using var db = new AppDbContext(options); await db.Database.EnsureDeletedAsync(); Console.WriteLine("Banco temporário de pedidos removido."); }
        }
    }

    private sealed class FakeCartHandler : HttpMessageHandler
    {
        public static decimal Price { get; set; } = 25m;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath != "/api/cart/order-snapshot" || request.Headers.Authorization?.Scheme != "Bearer")
                throw new InvalidOperationException("Consulta autenticada do carrinho não foi encaminhada.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                couponCode = "VSHOP10", items = new[] { new { productId = 1, productName = "Produto", productDescription = "Teste", quantity = 3, unitPrice = Price } }
            }) });
        }
    }
}
