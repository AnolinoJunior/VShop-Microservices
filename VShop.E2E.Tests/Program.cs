using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using IdentityDb = Vshop.IdentityServer.Data.ApplicationDbContext;
using ProductDb = Vshop.ProductApi.Context.AppDbContext;
using CartDb = VShop.CartiApi.Context.AppDbContext;
using OrderDb = VShop.OrderApi.Context.AppDbContext;
using DiscountDb = VShop.DiscountApi.Context.AppDbContext;

var root = Environment.GetEnvironmentVariable("VSHOP_E2E_ROOT") ?? Directory.GetCurrentDirectory();
var baseConnection = Environment.GetEnvironmentVariable("VSHOP_E2E_SQL") ?? throw new InvalidOperationException("Defina VSHOP_E2E_SQL.");
var nonce = Guid.NewGuid().ToString("N");
var webClientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) + "+/=";
var serviceClientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) + "+/=";
var testAdminPassword = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
var testClientPassword = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
var oldSagaSecret = Environment.GetEnvironmentVariable("VSHOP_SAGA_CLIENT_SECRET");
var useUserSecrets = Environment.GetEnvironmentVariable("VSHOP_E2E_USE_USER_SECRETS") == "1";
Environment.SetEnvironmentVariable("VSHOP_SAGA_CLIENT_SECRET", useUserSecrets ? null : Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) + "+/=");
var names = new[] { "VShopE2EIdentity_" + nonce, "VShopE2EProduct_" + nonce, "VShopE2ECart_" + nonce, "VShopE2EOrder_" + nonce, "VShopE2EDiscount_" + nonce, "VShopE2EIdentityProduction_" + nonce };
string Connection(string name) => new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = name, Pooling = false }.ConnectionString;
var masterConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master", Pooling = false }.ConnectionString;
var created = new List<string>();
var processes = new List<Process>();
var testParent = Path.GetFullPath(Path.Combine(root, "VShop.E2E.Tests"));
var testDirectory = Path.GetFullPath(Path.Combine(testParent, "run-" + nonce));
if (!string.Equals(Path.GetDirectoryName(testDirectory), testParent, StringComparison.OrdinalIgnoreCase) ||
    Path.GetFileName(testDirectory) != "run-" + nonce)
    throw new InvalidOperationException("Diretório temporário de teste inválido.");
Directory.CreateDirectory(testDirectory);
var passed = 0;
var failed = 0;
void Check(bool ok, string name) { if (ok) { passed++; Console.WriteLine("PASSOU: " + name); } else { failed++; Console.WriteLine("FALHOU: " + name); } }
int Port() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
string Url(int port) => $"https://localhost:{port}";
var identityUrl = Url(Port()); var productUrl = Url(Port()); var cartUrl = Url(Port()); var orderUrl = Url(Port()); var discountUrl = Url(Port());

Process Start(string project, string url, string dbName, Dictionary<string, string> settings)
{
    var path = Path.Combine(root, project, "bin", "Debug", "net8.0", project + ".dll");
    if (!File.Exists(path)) throw new FileNotFoundException("Compile o projeto antes do teste.", path);
    var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = testDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(path);
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["ASPNETCORE_URLS"] = url;
    info.Environment["ConnectionStrings__DefaultConnection"] = Connection(dbName);
    info.Environment["Logging__LogLevel__Default"] = "Warning";
    foreach (var entry in settings) info.Environment[entry.Key] = entry.Value;
    var process = Process.Start(info) ?? throw new InvalidOperationException("Falha ao iniciar " + project);
    process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
    process.BeginOutputReadLine(); process.BeginErrorReadLine(); processes.Add(process); return process;
}
async Task Ready(string url, string path, Process process)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    for (var i = 0; i < 60; i++)
    {
        if (process.HasExited) throw new InvalidOperationException($"Processo {url} encerrou com código {process.ExitCode}.");
        try { using var r = await client.GetAsync(url + path); if ((int)r.StatusCode < 500) return; }
        catch (HttpRequestException) { } catch (TaskCanceledException) { }
        await Task.Delay(500);
    }
    throw new InvalidOperationException("Serviço não ficou pronto: " + url);
}
string Attr(string html, string field)
{
    foreach (Match match in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
    {
        var tag = match.Value;
        if (!Regex.IsMatch(tag, "\\bname=\"" + Regex.Escape(field) + "\"", RegexOptions.IgnoreCase)) continue;
        var value = Regex.Match(tag, "\\bvalue=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return value.Success ? WebUtility.HtmlDecode(value.Groups[1].Value) : "";
    }
    throw new InvalidOperationException("Campo não encontrado na página de login: " + field);
}
string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

try
{
    await using (var sql = new SqlConnection(masterConnection))
    {
        await sql.OpenAsync();
        foreach (var name in names)
        {
            await using var command = sql.CreateCommand(); command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync(); created.Add(name);
        }
    }
    // EnsureCreated cria o esquema diretamente nos bancos exclusivos, sem executar migrations.
    await using (var db = new IdentityDb(new DbContextOptionsBuilder<IdentityDb>().UseSqlServer(Connection(names[0])).Options)) await db.Database.EnsureCreatedAsync();
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options)) await db.Database.EnsureCreatedAsync();
    await using (var db = new CartDb(new DbContextOptionsBuilder<CartDb>().UseSqlServer(Connection(names[2])).Options)) await db.Database.EnsureCreatedAsync();
    await using (var db = new OrderDb(new DbContextOptionsBuilder<OrderDb>().UseSqlServer(Connection(names[3])).Options)) await db.Database.EnsureCreatedAsync();
    await using (var db = new DiscountDb(new DbContextOptionsBuilder<DiscountDb>().UseSqlServer(Connection(names[4])).Options)) await db.Database.EnsureCreatedAsync();
    await using (var db = new IdentityDb(new DbContextOptionsBuilder<IdentityDb>().UseSqlServer(Connection(names[5])).Options)) await db.Database.EnsureCreatedAsync();
    var identitySettings = new Dictionary<string, string>
    {
        ["OAuth__WebClientSecret"] = webClientSecret,
        ["OAuth__ServiceClientSecret"] = serviceClientSecret,
        ["DevelopmentSeed__AdminPassword"] = testAdminPassword,
        ["DevelopmentSeed__ClientPassword"] = testClientPassword
    };
    var identity = Start("Vshop.IdentityServer", identityUrl, names[0], identitySettings);
    await Ready(identityUrl, "/.well-known/openid-configuration", identity);
    var productionSettings = new Dictionary<string, string>(identitySettings) { ["ASPNETCORE_ENVIRONMENT"] = "Production" };
    var productionUrl = Url(Port());
    var productionIdentity = Start("Vshop.IdentityServer", productionUrl, names[5], productionSettings);
    await Ready(productionUrl, "/.well-known/openid-configuration", productionIdentity);
    await using (var productionDb = new IdentityDb(new DbContextOptionsBuilder<IdentityDb>().UseSqlServer(Connection(names[5])).Options))
        Check(await productionDb.Users.CountAsync() == 0, "produção não cria contas de desenvolvimento mesmo com senhas configuradas");
    var product = Start("Vshop.ProductApi", productUrl, names[1], new() { ["ServicesUri__IdentityServer"] = identityUrl });
    var discount = Start("VShop.DiscountApi", discountUrl, names[4], new() { ["ServicesUri__IdentityServer"] = identityUrl, ["ServicesUri__CartApi"] = cartUrl });
    var cart = Start("VShop.CartiApi", cartUrl, names[2], new() { ["ServicesUri__IdentityServer"] = identityUrl, ["ServicesUri__ProductApi"] = productUrl, ["ServicesUri__OrderApi"] = orderUrl, ["ServicesUri__DiscountApi"] = discountUrl });
    var order = Start("VShop.OrderApi", orderUrl, names[3], new() { ["IdentityServer__Authority"] = identityUrl, ["ServicesUri__CartApi"] = cartUrl, ["ServicesUri__ProductApi"] = productUrl, ["ServicesUri__DiscountApi"] = discountUrl });
    await Ready(productUrl, "/api/products", product);
    await Ready(cartUrl, "/api/cart/order-snapshot", cart);
    await Ready(orderUrl, "/api/order", order);
    await Ready(discountUrl, "/api/coupon", discount);

    using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    async Task<string> Token(string scopes)
    {
        var verifier = B64(RandomNumberGenerator.GetBytes(48));
        var challenge = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        const string redirect = "https://localhost:7116/signin-oidc";
        var auth = $"{identityUrl}/connect/authorize?client_id=Vshop.Web&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&scope={Uri.EscapeDataString(scopes)}&code_challenge={challenge}&code_challenge_method=S256&state=e2etest";
        using var start = await http.GetAsync(auth);
        if (start.StatusCode != HttpStatusCode.Redirect || start.Headers.Location is null) throw new InvalidOperationException("Authorization endpoint não redirecionou para login.");
        var destination = start.Headers.Location;
        if (!destination.ToString().StartsWith(redirect, StringComparison.Ordinal))
        {
            var loginUrl = new Uri(new Uri(identityUrl), destination);
            using var loginPage = await http.GetAsync(loginUrl);
            var html = await loginPage.Content.ReadAsStringAsync();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Attr(html, "__RequestVerificationToken"), ["Input.ReturnUrl"] = Attr(html, "Input.ReturnUrl"),
                ["Input.Username"] = "client1", ["Input.Password"] = testClientPassword, ["Input.Button"] = "login"
            });
            using var login = await http.PostAsync(loginUrl, form);
            if (login.StatusCode != HttpStatusCode.Redirect || login.Headers.Location is null) throw new InvalidOperationException("Login de teste falhou.");
            using var authorize = await http.GetAsync(new Uri(new Uri(identityUrl), login.Headers.Location));
            destination = authorize.Headers.Location;
            if (authorize.StatusCode != HttpStatusCode.Redirect || destination is null || !destination.ToString().StartsWith(redirect, StringComparison.Ordinal))
                throw new InvalidOperationException("IdentityServer não retornou código de autorização.");
        }
        var code = System.Web.HttpUtility.ParseQueryString(destination.Query)["code"] ?? throw new InvalidOperationException("Código OAuth ausente.");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, identityUrl + "/connect/token");
        var credentials = Uri.EscapeDataString("Vshop.Web") + ":" + Uri.EscapeDataString(webClientSecret);
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier });
        using var tokenResponse = await http.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode) throw new InvalidOperationException("Troca do código OAuth falhou: " + (int)tokenResponse.StatusCode);
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        return tokenJson.RootElement.GetProperty("access_token").GetString()!;
    }
    var token = await Token("openid Vshop.ProductApi Vshop.CartApi Vshop.OrderApi");
    using (var serviceTokenRequest = new HttpRequestMessage(HttpMethod.Post, identityUrl + "/connect/token"))
    {
        var credentials = Uri.EscapeDataString("client") + ":" + Uri.EscapeDataString(serviceClientSecret);
        serviceTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        serviceTokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["scope"] = "Vshop.ProductApi" });
        using var response = await http.SendAsync(serviceTokenRequest);
        Check(response.IsSuccessStatusCode, "cliente de serviço autentica com segredo de teste fornecido pela configuração");
    }
    var jwtBody = token.Split('.')[1].Replace('-', '+').Replace('_', '/'); jwtBody += new string('=', (4 - jwtBody.Length % 4) % 4);
    using var claims = JsonDocument.Parse(Convert.FromBase64String(jwtBody));
    var userId = claims.RootElement.GetProperty("sub").GetString()!;
    var scopesClaim = claims.RootElement.GetProperty("scope");
    Check(scopesClaim.ToString().Contains("Vshop.CartApi") && scopesClaim.ToString().Contains("Vshop.OrderApi") && scopesClaim.ToString().Contains("Vshop.ProductApi"), "IdentityServer emite JWT de usuário com os três escopos");
    int productId;
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options))
    {
        var item = new Vshop.ProductApi.Models.Product { Name = "E2E " + nonce[..8], Price = 31.25m, Stock = 7, CategoryId = 1, Description = "Teste E2E", ImageUrl = "e2e.jpg" };
        db.Products.Add(item); await db.SaveChangesAsync(); productId = item.ProductId;
    }
    await using (var db = new CartDb(new DbContextOptionsBuilder<CartDb>().UseSqlServer(Connection(names[2])).Options))
    {
        db.Products.Add(new VShop.CartiApi.Models.Product { ProductId = productId, Name = "Preço antigo", Price = 0.01m, Stock = 7, CategoryName = "Teste" });
        var header = new VShop.CartiApi.Models.CartHeader { UserId = userId, CouponCode = "VSHOP10" };
        db.CartHeaders.Add(header); await db.SaveChangesAsync();
        db.CartItems.Add(new VShop.CartiApi.Models.CartItem { CartHeaderId = header.Id, ProductId = productId, Quantity = 2 }); await db.SaveChangesAsync();
    }
    async Task<HttpResponseMessage> Send(HttpMethod method, string url, string? bearer, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request);
    }
    using (var noAuth = await Send(HttpMethod.Get, cartUrl + "/api/cart/order-snapshot", null)) Check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "CartiApi rejeita chamada sem JWT");
    using (var noAuth = await Send(HttpMethod.Post, orderUrl + "/api/order/prepare", null, new { })) Check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "OrderApi rejeita chamada sem JWT");
    var cartOnly = await Token("openid Vshop.CartApi");
    var orderOnly = await Token("openid Vshop.OrderApi");
    using (var wrongScope = await Send(HttpMethod.Get, cartUrl + "/api/cart/order-snapshot", orderOnly))
        Check(wrongScope.StatusCode == HttpStatusCode.Forbidden, "CartiApi rejeita JWT sem escopo CartApi");
    using (var wrongScope = await Send(HttpMethod.Post, orderUrl + "/api/order/prepare", cartOnly, new { }))
        Check(wrongScope.StatusCode == HttpStatusCode.Forbidden, "OrderApi rejeita JWT sem escopo OrderApi");
    using (var wrongScope = await Send(HttpMethod.Post, productUrl + "/api/stock-reservations", cartOnly,
        new { reservationId = Guid.NewGuid(), productId, quantity = 1 }))
        Check(wrongScope.StatusCode == HttpStatusCode.Forbidden, "ProductApi rejeita JWT sem escopo ProductApi");
    using (var forbidden = await Send(HttpMethod.Post, productUrl + "/api/internal/saga-stock-reservations", token,
        new { reservationId = Guid.NewGuid(), userId, productId, quantity = 1 }))
        Check(forbidden.StatusCode == HttpStatusCode.Forbidden, "usuário não acessa operações internas da Saga");
    using (var snapshot = await Send(HttpMethod.Get, cartUrl + "/api/cart/order-snapshot", token))
    {
        using var data = JsonDocument.Parse(await snapshot.Content.ReadAsStringAsync());
        var item = data.RootElement.GetProperty("items")[0];
        Check(snapshot.IsSuccessStatusCode && item.GetProperty("unitPrice").GetDecimal() == 31.25m && item.GetProperty("quantity").GetInt32() == 2, "CartiApi consulta preço atual da ProductApi real");
    }
    var key = "e2e-" + nonce;
    object Payload(string first) => new { firstName = first, lastName = "Teste", email = "e2e@example.com", telephone = "11999999999", address = "Rua Teste", idempotencyKey = key };
    using (var wrongScope = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", cartOnly, Payload("E2E")))
        Check(wrongScope.StatusCode == HttpStatusCode.Forbidden, "CartiApi encaminha JWT e OrderApi exige seu escopo");
    using (var wrongScope = await Send(HttpMethod.Post, orderUrl + "/api/order/prepare", orderOnly, Payload("E2E")))
        Check(wrongScope.StatusCode == HttpStatusCode.ServiceUnavailable, "OrderApi não obtém snapshot quando JWT não possui escopo CartApi");
    long firstId = 0;
    using (var first = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", token, Payload("E2E")))
    {
        var content = await first.Content.ReadAsStringAsync();
        Check(first.StatusCode == HttpStatusCode.Created, "POST CartiApi cria pedido Pending com 201");
        if (first.IsSuccessStatusCode)
        {
            using var json = JsonDocument.Parse(content); firstId = json.RootElement.GetProperty("orderHeaderId").GetInt64();
            Check(json.RootElement.GetProperty("subtotalAmount").GetDecimal() == 62.50m && json.RootElement.GetProperty("status").GetInt32() == 0, "OrderApi usou snapshot real e gravou subtotal Pending");
        }
        else Console.WriteLine("Detalhe HTTP: " + (int)first.StatusCode + " " + content);
    }
    using (var repeat = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", token, Payload("E2E")))
    {
        var content = await repeat.Content.ReadAsStringAsync();
        var same = false; if (repeat.IsSuccessStatusCode) { using var json = JsonDocument.Parse(content); same = json.RootElement.GetProperty("orderHeaderId").GetInt64() == firstId && firstId > 0; }
        Check(repeat.StatusCode == HttpStatusCode.OK && same, "mesma chave retorna 200 e mesmo pedido");
    }
    using (var different = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", token, Payload("Outro"))) Check(different.StatusCode == HttpStatusCode.Conflict, "mesma chave com dados diferentes retorna 409");
    await using (var db = new OrderDb(new DbContextOptionsBuilder<OrderDb>().UseSqlServer(Connection(names[3])).Options))
    {
        var orders = await db.OrderHeaders.Include(o => o.OrderDetails).Where(o => o.UserId == userId && o.IdempotencyKey == key).ToListAsync();
        Check(orders.Count == 1 && orders[0].OrderDetails.Count == 1 && orders[0].OrderDetails.Single().UnitPrice == 31.25m, "SQL temporário contém um único pedido e detalhes corretos");
    }
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options))
        Check((await db.Products.FindAsync(productId))!.Stock == 7 && await db.StockReservations.CountAsync() == 0, "estoque e reservas de estoque permanecem inalterados");
    await using (var db = new DiscountDb(new DbContextOptionsBuilder<DiscountDb>().UseSqlServer(Connection(names[4])).Options))
        Check(await db.CouponReservations.CountAsync() == 0 && (await db.Coupons.SingleAsync(c => c.CouponCode == "VSHOP10")).IsActive, "cupom e reservas de cupom permanecem inalterados");
    await using (var db = new CartDb(new DbContextOptionsBuilder<CartDb>().UseSqlServer(Connection(names[2])).Options))
    {
        var header = await db.CartHeaders.SingleAsync(c => c.UserId == userId); var item = await db.CartItems.SingleAsync(i => i.CartHeaderId == header.Id);
        Check(header.CouponCode == "VSHOP10" && item.Quantity == 2 && item.ProductId == productId, "carrinho e cupom aplicado permanecem inalterados");
    }
    async Task<(string Phase, string Payment)> Checkout(long id, bool decline)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var response = await Send(HttpMethod.Post, cartUrl + $"/api/cart/orders/{id}/checkout", token, new { simulateDecline = decline });
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Checkout HTTP {(int)response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            var phase = json.RootElement.GetProperty("phase").GetString()!;
            var payment = json.RootElement.GetProperty("paymentStatus").GetString()!;
            if (phase is "Completed" or "Failed" or "Reconciliation") return (phase, payment);
            if (attempt == 19) Console.WriteLine("Checkout pendente: " + text);
            await Task.Delay(1000);
        }
        return ("Timeout", "Unknown");
    }
    var simultaneous = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
        Send(HttpMethod.Post, cartUrl + $"/api/cart/orders/{firstId}/checkout", token, new { simulateDecline = false })));
    try { Check(simultaneous.All(r => r.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK),
        "requisições simultâneas de checkout são aceitas sem conflito indevido"); }
    finally { foreach (var response in simultaneous) response.Dispose(); }
    var approved = await Checkout(firstId, false);
    Check(approved == ("Completed", "Approved"), "Saga real confirma pedido após reservas e pagamento aprovado");
    var repeated = await Checkout(firstId, false);
    Check(approved.Phase == "Completed" && repeated == approved, "repetição do checkout confirmado é idempotente");
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options))
        Check((await db.Products.FindAsync(productId))!.Stock == 5 && await db.StockReservations.CountAsync(r => r.Status == Vshop.ProductApi.Models.ReservationStatus.Confirmed) == 1,
            "checkout aprovado confirma uma única reserva e baixa estoque uma vez");
    await using (var db = new DiscountDb(new DbContextOptionsBuilder<DiscountDb>().UseSqlServer(Connection(names[4])).Options))
        Check(await db.CouponReservations.CountAsync(r => r.Status == VShop.DiscountApi.Models.CouponReservationStatus.Confirmed) == 1,
            "checkout aprovado confirma uma única utilização do cupom");
    await using (var db = new OrderDb(new DbContextOptionsBuilder<OrderDb>().UseSqlServer(Connection(names[3])).Options))
    {
        var saved = await db.OrderHeaders.FindAsync(firstId);
        Check(saved?.Status == VShop.OrderApi.Models.OrderStatus.Confirmed && saved.DiscountAmount == 10m && saved.TotalAmount == 52.50m,
            "pedido confirmado contém desconto da reserva e total correto");
    }
    await using (var db = new CartDb(new DbContextOptionsBuilder<CartDb>().UseSqlServer(Connection(names[2])).Options))
    {
        var header = await db.CartHeaders.SingleAsync(c => c.UserId == userId); header.CouponCode = "VSHOP20"; await db.SaveChangesAsync();
    }
    var declineKey = "decline-" + nonce;
    using var declineOrder = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", token,
        new { firstName = "E2E", lastName = "Teste", email = "e2e@example.com", telephone = "11999999999", address = "Rua Teste", idempotencyKey = declineKey });
    if (declineOrder.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Não foi possível preparar cenário de recusa.");
    using var declineJson = JsonDocument.Parse(await declineOrder.Content.ReadAsStringAsync());
    var declineId = declineJson.RootElement.GetProperty("orderHeaderId").GetInt64();
    var declined = await Checkout(declineId, true);
    Check(declined == ("Failed", "Declined"), "pagamento recusado encerra pedido após compensação");
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options))
        Check((await db.Products.FindAsync(productId))!.Stock == 5 && await db.StockReservations.CountAsync(r => r.Status == Vshop.ProductApi.Models.ReservationStatus.Released) == 1,
            "recusa devolve estoque reservado");
    await using (var db = new DiscountDb(new DbContextOptionsBuilder<DiscountDb>().UseSqlServer(Connection(names[4])).Options))
        Check(await db.CouponReservations.CountAsync(r => r.Status == VShop.DiscountApi.Models.CouponReservationStatus.Released) == 1,
            "recusa libera reserva de cupom");
    var restartKey = "restart-" + nonce;
    using var restartOrder = await Send(HttpMethod.Post, cartUrl + "/api/cart/orders", token,
        new { firstName = "E2E", lastName = "Teste", email = "e2e@example.com", telephone = "11999999999", address = "Rua Teste", idempotencyKey = restartKey });
    if (restartOrder.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Não foi possível preparar cenário de reinício.");
    using var restartJson = JsonDocument.Parse(await restartOrder.Content.ReadAsStringAsync());
    var restartId = restartJson.RootElement.GetProperty("orderHeaderId").GetInt64();
    discount.Kill(entireProcessTree: true); await discount.WaitForExitAsync();
    using (var interrupted = await Send(HttpMethod.Post, cartUrl + $"/api/cart/orders/{restartId}/checkout", token, new { simulateDecline = false }))
        Check(interrupted.StatusCode == HttpStatusCode.Accepted, "indisponibilidade remota mantém Saga pendente");
    await using (var db = new OrderDb(new DbContextOptionsBuilder<OrderDb>().UseSqlServer(Connection(names[3])).Options))
    {
        var pending = await db.CheckoutSagas.Include(s => s.Order).SingleAsync(s => s.OrderHeaderId == restartId);
        Check(pending.Phase == VShop.OrderApi.Models.SagaPhase.Reserving && pending.Order.Status == VShop.OrderApi.Models.OrderStatus.Pending,
            "falha de rede persiste progresso sem confirmar ou falhar pedido");
    }
    order.Kill(entireProcessTree: true); await order.WaitForExitAsync();
    discount = Start("VShop.DiscountApi", discountUrl, names[4], new() { ["ServicesUri__IdentityServer"] = identityUrl, ["ServicesUri__CartApi"] = cartUrl });
    order = Start("VShop.OrderApi", orderUrl, names[3], new() { ["IdentityServer__Authority"] = identityUrl, ["ServicesUri__CartApi"] = cartUrl, ["ServicesUri__ProductApi"] = productUrl, ["ServicesUri__DiscountApi"] = discountUrl });
    await Ready(discountUrl, "/api/coupon", discount);
    await Ready(orderUrl, "/api/order", order);
    var recovered = false;
    for (var attempt = 0; attempt < 30 && !recovered; attempt++)
    {
        await Task.Delay(2000);
        await using var db = new OrderDb(new DbContextOptionsBuilder<OrderDb>().UseSqlServer(Connection(names[3])).Options);
        var state = await db.CheckoutSagas.AsNoTracking().SingleAsync(s => s.OrderHeaderId == restartId);
        recovered = state.Phase == VShop.OrderApi.Models.SagaPhase.Completed;
    }
    Check(recovered, "worker recupera automaticamente Saga após reinício da OrderApi");
    await using (var db = new ProductDb(new DbContextOptionsBuilder<ProductDb>().UseSqlServer(Connection(names[1])).Options))
        Check((await db.Products.FindAsync(productId))!.Stock == 3 && await db.StockReservations.CountAsync(r => r.Status == Vshop.ProductApi.Models.ReservationStatus.Confirmed) == 2,
            "retomada após reinício confirma estoque uma única vez");
    Console.WriteLine($"RESULTADO: {passed} passaram; {failed} falharam.");
}
finally
{
    Environment.SetEnvironmentVariable("VSHOP_SAGA_CLIENT_SECRET", oldSagaSecret);
    foreach (var process in processes) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } finally { process.Dispose(); } }
    await using (var sql = new SqlConnection(masterConnection))
    {
        await sql.OpenAsync();
        foreach (var name in created)
        {
            await using var command = sql.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }
    }
    Directory.Delete(testDirectory, recursive: true);
    Console.WriteLine("Serviços de teste encerrados e bancos temporários removidos.");
}
return failed == 0 ? 0 : 1;
