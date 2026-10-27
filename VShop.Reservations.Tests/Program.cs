using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vshop.ProductApi.Services;
using VShop.DiscountApi.Services;
using ProductContext = Vshop.ProductApi.Context.AppDbContext;
using DiscountContext = VShop.DiscountApi.Context.AppDbContext;
using StockStatus = Vshop.ProductApi.Models.ReservationStatus;
using CouponStatus = VShop.DiscountApi.Models.CouponReservationStatus;

var connection = Environment.GetEnvironmentVariable("VSHOP_RESERVATION_TEST_SQL")
    ?? throw new InvalidOperationException("Defina VSHOP_RESERVATION_TEST_SQL para uma instância SQL Server com permissão para criar bancos temporários.");
var suffix = Guid.NewGuid().ToString("N");
var stockName = "VShopStockTest_" + suffix;
var couponName = "VShopCouponTest_" + suffix;
var masterBuilder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false };
var stockBuilder = new SqlConnectionStringBuilder(connection) { InitialCatalog = stockName, Pooling = false };
var couponBuilder = new SqlConnectionStringBuilder(connection) { InitialCatalog = couponName, Pooling = false };
var stockOptions = new DbContextOptionsBuilder<ProductContext>().UseSqlServer(stockBuilder.ConnectionString).Options;
var couponOptions = new DbContextOptionsBuilder<DiscountContext>().UseSqlServer(couponBuilder.ConnectionString).Options;
var created = new List<string>();
var passed = 0;
var failed = 0;
var now = DateTime.UtcNow;

ProductContext StockDb() => new(stockOptions);
DiscountContext CouponDb() => new(couponOptions);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Run(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASSOU: {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FALHOU: {name}: {ex.GetType().Name}: {ex.Message}"); }
}
async Task ExpectConflict(Func<Task> operation)
{
    try { await operation(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("A operação deveria ter sido rejeitada.");
}
async Task<int> NewProduct(long stock = 1)
{
    await using var db = StockDb();
    var product = new Vshop.ProductApi.Models.Product { Name = "Produto de teste", Price = 50, Stock = stock, CategoryId = 1, ImageUrl = "test" };
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return product.ProductId;
}
async Task<long> Stock(int id)
{
    await using var db = StockDb();
    return await db.Products.Where(p => p.ProductId == id).Select(p => p.Stock).SingleAsync();
}
string User() => Guid.NewGuid().ToString("N");

try
{
    // Os nomes são gerados neste processo; somente bancos criados com sucesso entram na lista de limpeza.
    await using (var master = new SqlConnection(masterBuilder.ConnectionString))
    {
        await master.OpenAsync();
        foreach (var name in new[] { stockName, couponName })
        {
            await using var command = master.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
            created.Add(name);
        }
    }
    await using (var db = StockDb()) await db.Database.MigrateAsync();
    await using (var db = CouponDb()) await db.Database.MigrateAsync();
    Console.WriteLine("Bancos temporários criados e migrations aplicadas.");

    await Run("estoque: reserva e repetição idempotente", async () =>
    {
        var product = await NewProduct(3); var id = Guid.NewGuid(); var user = User();
        await using var db = StockDb(); var service = new StockReservationService(db);
        var first = await service.Reserve(id, product, user, 2, now);
        var second = await service.Reserve(id, product, user, 2, now);
        Check(first?.Status == StockStatus.Pending && second?.Id == id && await Stock(product) == 1, "Reserva repetida alterou o estoque.");
    });
    await Run("estoque: concorrência pela última unidade", async () =>
    {
        var product = await NewProduct();
        async Task<bool> Reserve()
        {
            await using var db = StockDb();
            try { return await new StockReservationService(db).Reserve(Guid.NewGuid(), product, User(), 1, now) != null; }
            catch (InvalidOperationException ex) when (ex.Message == "Estoque insuficiente.") { return false; }
        }
        var result = await Task.WhenAll(Reserve(), Reserve());
        Check(result.Count(x => x) == 1 && await Stock(product) == 0, "Mais de uma reserva obteve a mesma unidade.");
    });
    await Run("estoque: repetição simultânea da mesma reserva", async () =>
    {
        var product = await NewProduct(); var id = Guid.NewGuid(); var user = User();
        async Task<StockStatus?> Reserve()
        {
            await using var db = StockDb();
            return (await new StockReservationService(db).Reserve(id, product, user, 1, now))?.Status;
        }
        var results = await Task.WhenAll(Reserve(), Reserve());
        Check(results.All(x => x == StockStatus.Pending) && await Stock(product) == 0, "Repetições simultâneas falharam ou alteraram o estoque duas vezes.");
    });
    await Run("estoque: confirmação idempotente e proteção contra liberação", async () =>
    {
        var product = await NewProduct(); var id = Guid.NewGuid(); var user = User();
        await using var db = StockDb(); var service = new StockReservationService(db);
        await service.Reserve(id, product, user, 1, now);
        Check((await service.Change(id, user, true, now))?.Status == StockStatus.Confirmed, "Confirmação falhou.");
        Check((await service.Change(id, user, true, now))?.Status == StockStatus.Confirmed, "Repetição da confirmação falhou.");
        Check((await service.Change(id, user, false, now))?.Status == StockStatus.Confirmed && await Stock(product) == 0, "Estoque confirmado foi devolvido.");
    });
    await Run("estoque: liberação idempotente devolve uma unidade", async () =>
    {
        var product = await NewProduct(); var id = Guid.NewGuid(); var user = User();
        await using var db = StockDb(); var service = new StockReservationService(db);
        await service.Reserve(id, product, user, 1, now);
        Check((await service.Change(id, user, false, now))?.Status == StockStatus.Released, "Liberação falhou.");
        Check((await service.Change(id, user, false, now))?.Status == StockStatus.Released && await Stock(product) == 1, "Repetição devolveu estoque novamente.");
        Check((await service.Change(id, user, true, now))?.Status == StockStatus.Released, "Reserva liberada foi confirmada.");
    });
    await Run("estoque: expiração devolve estoque e impede confirmação", async () =>
    {
        var product = await NewProduct(); var id = Guid.NewGuid(); var user = User();
        await using var db = StockDb(); var service = new StockReservationService(db);
        await service.Reserve(id, product, user, 1, now.AddMinutes(-16));
        Check((await service.Change(id, user, true, now))?.Status == StockStatus.Expired && await Stock(product) == 1, "Expiração não devolveu o estoque.");
    });
    await Run("estoque: rotina de expiração preserva reserva confirmada", async () =>
    {
        var product = await NewProduct(2); var pending = Guid.NewGuid(); var confirmed = Guid.NewGuid(); var user = User();
        await using (var db = StockDb())
        {
            var service = new StockReservationService(db);
            await service.Reserve(pending, product, user, 1, now.AddMinutes(-16));
            await service.Reserve(confirmed, product, user, 1, now.AddMinutes(-16));
            await service.Change(confirmed, user, true, now.AddMinutes(-15));
        }
        await using (var db = StockDb()) await new StockReservationService(db).ExpireBatch(now);
        await using var verify = StockDb();
        Check((await verify.StockReservations.FindAsync(pending))?.Status == StockStatus.Expired &&
              (await verify.StockReservations.FindAsync(confirmed))?.Status == StockStatus.Confirmed && await Stock(product) == 1, "Rotina alterou reserva confirmada ou não expirou a pendente.");
    });
    await Run("estoque: reserva preparada não expira e só a Saga pode concluí-la", async () =>
    {
        var product = await NewProduct(); var id = Guid.NewGuid(); var user = User();
        await using var db = StockDb(); var service = new StockReservationService(db);
        await service.Reserve(id, product, user, 1, now, saga: true);
        Check((await service.Change(id, user, true, now))?.Status == StockStatus.Pending, "Cliente confirmou reserva da Saga.");
        Check((await service.Change(id, user, true, now, saga: true))?.Status == StockStatus.Pending, "Reserva foi confirmada antes de preparar.");
        Check((await service.Prepare(id, user, now))?.Status == StockStatus.Prepared, "Preparação falhou.");
        Check((await service.Prepare(id, user, now))?.Status == StockStatus.Prepared, "Preparação não é idempotente.");
        Check((await service.Change(id, user, false, now.AddMinutes(20)))?.Status == StockStatus.Prepared, "Cliente liberou reserva preparada.");
        await service.ExpireBatch(now.AddMinutes(20));
        Check((await service.Change(id, user, true, now.AddMinutes(20), saga: true))?.Status == StockStatus.Confirmed && await Stock(product) == 0,
            "Reserva preparada expirou ou não foi confirmada.");
    });
    await Run("cupom: subtotal abaixo de R$ 50 é recusado", async () =>
    {
        await using var db = CouponDb();
        await ExpectConflict(async () => await new CouponReservationService(db).Reserve(Guid.NewGuid(), "VSHOP10", User(), 49.99m, now));
    });
    await Run("cupom: subtotal de R$ 50 é aceito e reserva é idempotente", async () =>
    {
        var id = Guid.NewGuid(); var user = User(); await using var db = CouponDb(); var service = new CouponReservationService(db);
        Check((await service.Reserve(id, "VSHOP10", user, 50m, now))?.Status == CouponStatus.Pending, "Reserva no mínimo falhou.");
        Check((await service.Reserve(id, "VSHOP10", user, 50m, now))?.Id == id, "Repetição da reserva falhou.");
    });
    await Run("cupom: concorrência para mesmo usuário", async () =>
    {
        var user = User();
        async Task<bool> Reserve()
        {
            await using var db = CouponDb();
            try { return await new CouponReservationService(db).Reserve(Guid.NewGuid(), "VSHOP10", user, 50, now) != null; }
            catch (InvalidOperationException ex) when (ex.Message == "O usuário já reservou ou utilizou este cupom.") { return false; }
        }
        var results = await Task.WhenAll(Reserve(), Reserve());
        Check(results.Count(x => x) == 1, "Usuário obteve duas reservas simultâneas.");
    });
    await Run("cupom: repetição simultânea da mesma reserva", async () =>
    {
        var id = Guid.NewGuid(); var user = User();
        async Task<CouponStatus?> Reserve()
        {
            await using var db = CouponDb();
            return (await new CouponReservationService(db).Reserve(id, "VSHOP10", user, 50, now))?.Status;
        }
        var results = await Task.WhenAll(Reserve(), Reserve());
        await using var verify = CouponDb();
        Check(results.All(x => x == CouponStatus.Pending) && await verify.CouponReservations.CountAsync(r => r.Id == id) == 1,
            "Repetições simultâneas falharam ou duplicaram a reserva.");
    });
    await Run("cupom: usuários diferentes podem reservar o mesmo código", async () =>
    {
        await using var db = CouponDb(); var service = new CouponReservationService(db);
        Check((await service.Reserve(Guid.NewGuid(), "VSHOP10", User(), 50, now))?.Status == CouponStatus.Pending, "Primeiro usuário falhou.");
        Check((await service.Reserve(Guid.NewGuid(), "VSHOP10", User(), 50, now))?.Status == CouponStatus.Pending, "Segundo usuário falhou.");
    });
    await Run("cupom: confirmação idempotente impede reutilização", async () =>
    {
        var id = Guid.NewGuid(); var user = User(); await using var db = CouponDb(); var service = new CouponReservationService(db);
        await service.Reserve(id, "VSHOP10", user, 50, now);
        Check((await service.Change(id, user, true, now))?.Status == CouponStatus.Confirmed, "Confirmação falhou.");
        Check((await service.Change(id, user, true, now))?.Status == CouponStatus.Confirmed, "Repetição da confirmação falhou.");
        Check((await service.Change(id, user, false, now))?.Status == CouponStatus.Confirmed, "Uso confirmado foi liberado.");
        await ExpectConflict(async () => await service.Reserve(Guid.NewGuid(), "VSHOP10", user, 50, now));
    });
    await Run("cupom: liberação idempotente permite nova reserva", async () =>
    {
        var id = Guid.NewGuid(); var user = User(); await using var db = CouponDb(); var service = new CouponReservationService(db);
        await service.Reserve(id, "VSHOP10", user, 50, now);
        Check((await service.Change(id, user, false, now))?.Status == CouponStatus.Released, "Liberação falhou.");
        Check((await service.Change(id, user, false, now))?.Status == CouponStatus.Released, "Repetição da liberação falhou.");
        Check((await service.Change(id, user, true, now))?.Status == CouponStatus.Released, "Cupom liberado foi confirmado.");
        Check((await service.Reserve(Guid.NewGuid(), "VSHOP10", user, 50, now))?.Status == CouponStatus.Pending, "Nova reserva falhou.");
    });
    await Run("cupom: expiração permite nova reserva e impede confirmação", async () =>
    {
        var id = Guid.NewGuid(); var user = User(); await using var db = CouponDb(); var service = new CouponReservationService(db);
        await service.Reserve(id, "VSHOP10", user, 50, now.AddMinutes(-16));
        Check((await service.Change(id, user, true, now))?.Status == CouponStatus.Expired, "Cupom expirado foi confirmado.");
        Check((await service.Reserve(Guid.NewGuid(), "VSHOP10", user, 50, now))?.Status == CouponStatus.Pending, "Cupom expirado permaneceu bloqueado.");
    });
    await Run("cupom: rotina de expiração preserva utilização confirmada", async () =>
    {
        var pending = Guid.NewGuid(); var confirmed = Guid.NewGuid(); var user = User();
        await using (var db = CouponDb())
        {
            var service = new CouponReservationService(db);
            await service.Reserve(pending, "VSHOP10", user, 50, now.AddMinutes(-16));
            await service.Reserve(confirmed, "VSHOP10", User(), 50, now.AddMinutes(-16));
            var owner = (await db.CouponReservations.FindAsync(confirmed))!.UserId;
            await service.Change(confirmed, owner, true, now.AddMinutes(-15));
        }
        await using (var db = CouponDb()) await new CouponReservationService(db).ExpireBatch(now);
        await using var verify = CouponDb();
        Check((await verify.CouponReservations.FindAsync(pending))?.Status == CouponStatus.Expired &&
              (await verify.CouponReservations.FindAsync(confirmed))?.Status == CouponStatus.Confirmed, "Rotina alterou utilização confirmada ou não expirou a pendente.");
    });
    await Run("cupom: reserva preparada impede duplicação e não expira", async () =>
    {
        var id = Guid.NewGuid(); var user = User(); await using var db = CouponDb(); var service = new CouponReservationService(db);
        await service.Reserve(id, "VSHOP10", user, 50, now, saga: true);
        Check((await service.Change(id, user, true, now))?.Status == CouponStatus.Pending, "Cliente confirmou reserva da Saga.");
        Check((await service.Change(id, user, true, now, saga: true))?.Status == CouponStatus.Pending, "Reserva foi confirmada antes de preparar.");
        Check((await service.Prepare(id, user, now))?.Status == CouponStatus.Prepared, "Preparação falhou.");
        Check((await service.Prepare(id, user, now))?.Status == CouponStatus.Prepared, "Preparação não é idempotente.");
        Check((await service.Change(id, user, false, now.AddMinutes(20)))?.Status == CouponStatus.Prepared, "Cliente liberou reserva preparada.");
        await ExpectConflict(async () => await service.Reserve(Guid.NewGuid(), "VSHOP10", user, 50, now.AddMinutes(20)));
        await service.ExpireBatch(now.AddMinutes(20));
        Check((await service.Change(id, user, true, now.AddMinutes(20), saga: true))?.Status == CouponStatus.Confirmed, "Cupom preparado não foi confirmado.");
    });
}
finally
{
    foreach (var name in created)
    {
        await using var db = name == stockName ? (DbContext)StockDb() : CouponDb();
        await db.Database.EnsureDeletedAsync();
        Console.WriteLine($"Banco temporário removido: {name}");
    }
}
Console.WriteLine($"RESULTADO: {passed} passaram; {failed} falharam.");
return failed == 0 ? 0 : 1;
