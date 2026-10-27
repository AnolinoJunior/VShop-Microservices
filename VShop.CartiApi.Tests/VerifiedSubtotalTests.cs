using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VShop.CartiApi.Context;
using VShop.CartiApi.Models;
using VShop.CartiApi.Repositories;
using VShop.CartiApi.Controllers;
using VShop.CartiApi.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

internal static class VerifiedSubtotalTests
{
    public static async Task Run(string connection)
    {
        var databaseName = "VShopCartPriceTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false };
        var testDatabase = new SqlConnectionStringBuilder(connection) { InitialCatalog = databaseName, Pooling = false };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(testDatabase.ConnectionString).Options;
        var created = false;
        try
        {
            await using (var sql = new SqlConnection(master.ConnectionString))
            {
                await sql.OpenAsync();
                await using var command = sql.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync();
                created = true;
            }
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            db.Products.Add(new Product { ProductId = 123, Name = "Produto", Price = 500m, CategoryName = "Teste" });
            var header = new CartHeader { UserId = "verified-user" };
            db.CartHeaders.Add(header);
            await db.SaveChangesAsync();
            db.CartItems.Add(new CartItem { CartHeaderId = header.Id, ProductId = 123, Quantity = 2 });
            await db.SaveChangesAsync();

            var client = new HttpClient(new ProductHandler()) { BaseAddress = new Uri("https://product.test/") };
            var repository = new CartRepository(db, null!, new SingleClientFactory(client), null!);
            var subtotal = await repository.GetVerifiedSubtotalAsync("verified-user");
            if (subtotal != 49.98m) throw new InvalidOperationException($"Subtotal verificado incorreto: {subtotal}.");
            Console.WriteLine("OK: subtotal verificado usa preço da ProductApi, não preço salvo no carrinho.");
            var snapshot = await repository.GetOrderSnapshotAsync("verified-user");
            if (snapshot?.Items.Single().UnitPrice != 24.99m || snapshot.Items.Single().Quantity != 2 ||
                snapshot.Items.Single().ProductName != "Nome da ProductApi")
                throw new InvalidOperationException("Snapshot não utilizou os dados verificados da ProductApi.");
            if (await repository.GetOrderSnapshotAsync("other-user") is not null)
                throw new InvalidOperationException("O snapshot acessou o carrinho de outro usuário.");
            if (await repository.GetOrderSnapshotAsync("VERIFIED-USER") is not null)
                throw new InvalidOperationException("O snapshot aceitou uma identidade diferente por causa da collation.");
            Console.WriteLine("OK: snapshot usa dados da ProductApi e respeita o usuário do carrinho.");
            var orderHandler = new OrderHandler();
            using var orderClient = new HttpClient(orderHandler) { BaseAddress = new Uri("http://order.test/") };
            var controller = new CartController(repository, new SingleClientFactory(orderClient));
            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "verified-user") }, "test"));
            http.Request.Headers.Authorization = "Bearer token-de-teste";
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            var response = await controller.PrepareOrder(new PrepareOrderDTO
            {
                FirstName = "Ana", LastName = "Silva", Email = "ana@example.com", Telephone = "11999999999", IdempotencyKey = "key"
            }, CancellationToken.None);
            if (response is not ContentResult { StatusCode: 201 } || !orderHandler.Received)
                throw new InvalidOperationException("CartiApi não encaminhou a solicitação autenticada à OrderApi.");
            http.User = new ClaimsPrincipal(new ClaimsIdentity());
            if (await controller.PrepareOrder(new PrepareOrderDTO(), CancellationToken.None) is not ForbidResult)
                throw new InvalidOperationException("CartiApi aceitou solicitação sem usuário autenticado.");
            Console.WriteLine("OK: preparação encaminha JWT à OrderApi e exige usuário autenticado.");
        }
        finally
        {
            if (created)
            {
                await using var db = new AppDbContext(options);
                await db.Database.EnsureDeletedAsync();
                Console.WriteLine("Banco temporário de subtotal removido.");
            }
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ProductHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath != "/api/products/123")
                throw new InvalidOperationException("Consulta inesperada à ProductApi.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"productId\":123,\"name\":\"Nome da ProductApi\",\"price\":24.99}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class OrderHandler : HttpMessageHandler
    {
        public bool Received { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri?.AbsolutePath != "/api/order/prepare" || request.Headers.Authorization?.ToString() != "Bearer token-de-teste" ||
                body.Contains("userId", StringComparison.OrdinalIgnoreCase) || body.Contains("unitPrice", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solicitação enviada à OrderApi é inválida.");
            Received = true;
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
