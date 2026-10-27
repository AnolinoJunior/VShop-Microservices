using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VShop.OrderApi.Context;
using VShop.OrderApi.Models;
using VShop.OrderApi.Services;

internal static class SagaRecoveryTests
{
    public static async Task Run(string connection)
    {
        var name = "VShopSagaTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false };
        var test = new SqlConnectionStringBuilder(connection) { InitialCatalog = name, Pooling = false };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(test.ConnectionString).Options;
        var created = false;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VSHOP_SAGA_CLIENT_SECRET"] = "test-only-secret-with-at-least-32-characters"
        }).Build();
        var server = new FakeServices();
        var factory = new FakeFactory(server);
        CheckoutSagaService Service(AppDbContext db) => new(db, new SagaRemoteClient(factory, config), new SimulatedPaymentService());
        void Check(bool ok, string error) { if (!ok) throw new Exception(error); }
        try
        {
            await using (var sql = new SqlConnection(master.ConnectionString))
            {
                await sql.OpenAsync(); await using var command = sql.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync(); created = true;
            }
            await using (var db = new AppDbContext(options)) await db.Database.MigrateAsync();
            long orderId;
            await using (var db = new AppDbContext(options))
            {
                var order = new OrderHeader { UserId = "saga-user", FirstName = "Ana", LastName = "Teste", Email = "a@example.com", Telephone = "11999999999", IdempotencyKey = "saga-1", CouponCode = "VSHOP10", SubtotalAmount = 80, TotalAmount = 80,
                    OrderDetails = new List<OrderDetail> { new() { ProductId = 11, ProductName = "Teste", Quantity = 2, UnitPrice = 40, LineTotal = 80 } } };
                db.OrderHeaders.Add(order); await db.SaveChangesAsync(); orderId = order.OrderHeaderId;
            }
            server.LoseStockConfirmation = true;
            await using (var db = new AppDbContext(options))
            {
                var result = await Service(db).Start(orderId, "saga-user", false, CancellationToken.None);
                Check(result?.Phase == SagaPhase.Committing && result.Order.Status != OrderStatus.Failed && server.StockConfirmed == 1,
                    "Resposta perdida após confirmação deve manter Saga em confirmação, sem falhar o pedido.");
            }
            Console.WriteLine("PASSOU: timeout após confirmação parcial preserva decisão e não marca Failed");
            await using (var db = new AppDbContext(options)) await Service(db).Process(orderId, CancellationToken.None);
            await using (var db = new AppDbContext(options))
            {
                var saga = await db.CheckoutSagas.Include(s => s.Order).Include(s => s.Items).SingleAsync(s => s.OrderHeaderId == orderId);
                Check(saga.Phase == SagaPhase.Completed && saga.Order.Status == OrderStatus.Confirmed && saga.CouponConfirmed && saga.Items.Single().Confirmed &&
                    server.StockCreated == 1 && server.StockConfirmed == 1 && server.CouponCreated == 1 && server.CouponConfirmed == 1,
                    "Retomada duplicou operação ou não concluiu o pedido.");
            }
            Console.WriteLine("PASSOU: reinício retoma confirmação parcial sem duplicar estoque ou cupom");
            long secondId;
            await using (var db = new AppDbContext(options))
            {
                var order = new OrderHeader { UserId = "another-user", FirstName = "Ana", LastName = "Teste", Email = "a@example.com", Telephone = "11999999999", IdempotencyKey = "saga-2", SubtotalAmount = 40, TotalAmount = 40,
                    OrderDetails = new List<OrderDetail> { new() { ProductId = 12, ProductName = "Teste", Quantity = 1, UnitPrice = 40, LineTotal = 40 } } };
                db.OrderHeaders.Add(order); await db.SaveChangesAsync(); secondId = order.OrderHeaderId;
            }
            var starts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var db = new AppDbContext(options);
                return await Service(db).Start(secondId, "another-user", false, CancellationToken.None);
            }));
            await using (var db = new AppDbContext(options)) await Service(db).Process(secondId, CancellationToken.None);
            await using (var db = new AppDbContext(options))
            {
                Check(await db.CheckoutSagas.CountAsync(s => s.OrderHeaderId == secondId) == 1 &&
                    (await db.OrderHeaders.FindAsync(secondId))!.Status == OrderStatus.Confirmed && server.StockCreated == 2 && server.StockConfirmed == 2,
                    "Concorrência criou mais de uma Saga ou duplicou estoque.");
            }
            Console.WriteLine("PASSOU: chamadas concorrentes são idempotentes e usam uma única reserva");
            long thirdId;
            await using (var db = new AppDbContext(options))
            {
                var order = new OrderHeader { UserId = "reconcile-user", FirstName = "Ana", LastName = "Teste", Email = "a@example.com", Telephone = "11999999999", IdempotencyKey = "saga-3", CouponCode = "VSHOP10", SubtotalAmount = 80, TotalAmount = 80,
                    OrderDetails = new List<OrderDetail> { new() { ProductId = 13, ProductName = "Teste", Quantity = 2, UnitPrice = 40, LineTotal = 80 } } };
                db.OrderHeaders.Add(order); await db.SaveChangesAsync(); thirdId = order.OrderHeaderId;
            }
            server.RejectCouponConfirmationOnce = true;
            await using (var db = new AppDbContext(options))
            {
                var result = await Service(db).Start(thirdId, "reconcile-user", false, CancellationToken.None);
                Check(result?.Phase == SagaPhase.Reconciliation && result.CommitDecision && result.Order.Status != OrderStatus.Failed,
                    "Falha parcial não preservou decisão de confirmação.");
            }
            await using (var db = new AppDbContext(options)) await Service(db).Process(thirdId, CancellationToken.None);
            await using (var db = new AppDbContext(options))
            {
                var result = await db.CheckoutSagas.Include(s => s.Order).SingleAsync(s => s.OrderHeaderId == thirdId);
                Check(result.Phase == SagaPhase.Completed && result.Order.Status == OrderStatus.Confirmed && server.StockConfirmed == 3 && server.CouponConfirmed == 2,
                    "Reconciliação não retomou a decisão de confirmação.");
            }
            Console.WriteLine("PASSOU: falha parcial é reconciliada sem liberar recurso confirmado");
            Console.WriteLine("RESULTADO recuperação: 4 passaram; 0 falharam.");
        }
        finally
        {
            if (created) { await using var db = new AppDbContext(options); await db.Database.EnsureDeletedAsync(); Console.WriteLine("Banco temporário da Saga removido."); }
        }
    }

    private sealed class FakeFactory(FakeServices handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri(name switch
        {
            "SagaIdentity" => "https://identity.test/", "SagaProduct" => "https://product.test/", _ => "https://discount.test/"
        }) };
    }

    private sealed class FakeServices : HttpMessageHandler
    {
        private readonly object gate = new();
        private readonly Dictionary<Guid, int> stock = new();
        private readonly Dictionary<Guid, int> coupon = new();
        public bool LoseStockConfirmation { get; set; }
        public bool RejectCouponConfirmationOnce { get; set; }
        public int StockCreated { get; private set; }
        public int StockConfirmed { get; private set; }
        public int CouponCreated { get; private set; }
        public int CouponConfirmed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "identity.test") return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "service-token" }) };
            if (request.Headers.Authorization?.Parameter != "service-token") return new HttpResponseMessage(HttpStatusCode.Forbidden);
            var isStock = request.RequestUri.Host == "product.test";
            var pieces = request.RequestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            lock (gate)
            {
                var data = isStock ? stock : coupon;
                var reserve = pieces.Length == 3;
                var id = reserve ? body.RootElement.GetProperty("reservationId").GetGuid() : Guid.Parse(pieces[3]);
                if (reserve && !data.ContainsKey(id))
                {
                    data[id] = 0;
                    if (isStock) StockCreated++; else CouponCreated++;
                }
                if (!data.TryGetValue(id, out var status)) return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (!reserve)
                {
                    var action = pieces[4];
                    if (!isStock && action == "confirm" && RejectCouponConfirmationOnce)
                    {
                        RejectCouponConfirmationOnce = false;
                        return new HttpResponseMessage(HttpStatusCode.Conflict);
                    }
                    if (action == "prepare" && status == 0) data[id] = status = 4;
                    if (action == "confirm" && status == 4)
                    {
                        data[id] = status = 1;
                        if (isStock) StockConfirmed++; else CouponConfirmed++;
                        if (isStock && LoseStockConfirmation) { LoseStockConfirmation = false; throw new HttpRequestException("Resposta de confirmação perdida após processamento."); }
                    }
                    if (action == "release" && status is 0 or 4) data[id] = status = 2;
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { status, discountAmount = 10m }) };
            }
        }
    }
}
