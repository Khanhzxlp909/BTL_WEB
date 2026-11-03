using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Web.Tests;

public sealed class SqlServerCommerceTests
{
    private sealed class Database : IAsyncDisposable
    {
        public DbContextOptions<BookStoreDbContext> Options { get; }
        public DatabaseReadiness Readiness { get; } = new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:BookStore"] = "IsolatedSqlTest" }).Build());
        public CheckoutQuotes Quotes { get; } = new(new EphemeralDataProtectionProvider());
        public Database()
        {
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BOOKSTORE_TEST_SQLSERVER"))
            { InitialCatalog = $"BookStoreTest_{Guid.NewGuid():N}" };
            Options = new DbContextOptionsBuilder<BookStoreDbContext>().UseSqlServer(connection.ConnectionString).Options;
        }
        public BookStoreDbContext Context() => new(Options);
        public ICartService Cart(BookStoreDbContext db) => new CartService(db, Readiness, new HttpContextAccessor());
        public IOrderService Orders(BookStoreDbContext db) => new OrderService(db, Readiness, Cart(db), Quotes);
        public async Task Seed()
        {
            await using var db = Context(); await db.Database.MigrateAsync();
            for (var n = 1; n <= 3; n++) db.Users.Add(new ApplicationUser { UserName = $"sql_user{n}", NormalizedUserName = $"SQL_USER{n}",
                Email = $"sql{n}@example.test", NormalizedEmail = $"SQL{n}@EXAMPLE.TEST", PasswordHash = "test-not-a-login" });
            db.Books.Add(new Book { Title = "SQL commerce", Author = "Test", SKU = "COM-001", Price = 100000,
                Category = new Category { Name = "Commerce test" }, Inventory = new Inventory { Quantity = 1 } });
            await db.SaveChangesAsync();
        }
        public async Task<CheckoutInput> Quote(int user)
        {
            await using var db = Context(); await Cart(db).AddAsync(user, new() { BookId = 1, Quantity = 1 }, default);
            var view = await Orders(db).CheckoutAsync(user, default);
            return new() { QuoteToken = view.Input.QuoteToken, RecipientName = "Test recipient", Phone = "0900000000", ShippingAddress = "Test address", ConfirmQuote = true };
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            Assert.StartsWith("BookStoreTest_", db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task LastCopy_TwoCustomers_OneOrderOnly_NoOversell()
    {
        await using var fixture = new Database(); await fixture.Seed();
        var first = await fixture.Quote(1); var second = await fixture.Quote(2);
        async Task<string> Place(int user, CheckoutInput input)
        {
            await using var db = fixture.Context();
            try { await fixture.Orders(db).PlaceAsync(user, input, default); return "saved"; }
            catch (AppException ex) { return ex.Error.Code; }
        }
        var results = await Task.WhenAll(Place(1, first), Place(2, second));
        Assert.Single(results, x => x == "saved"); Assert.Single(results, x => x == "CHECKOUT_STOCK_UNAVAILABLE");
        await using var verify = fixture.Context();
        Assert.Single(await verify.Orders.ToListAsync()); Assert.Equal(1, (await verify.Inventories.SingleAsync()).ReservedQuantity);
        Assert.Single(await verify.CartItems.ToListAsync()); Assert.Single(await verify.InventoryTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task ConfirmationReplay_ConcurrentRequestsCreateOneOrder_CancellationAndDeliveryIdempotent()
    {
        await using var fixture = new Database(); await fixture.Seed(); var quote = await fixture.Quote(1);
        async Task<int> Place()
        {
            await using var db = fixture.Context(); return (await fixture.Orders(db).PlaceAsync(1, quote, default)).Id;
        }
        var ids = await Task.WhenAll(Place(), Place()); Assert.Equal(ids[0], ids[1]);
        await using (var change = fixture.Context())
        {
            var service = fixture.Orders(change);
            await service.CancelAsync(1, false, ids[0], new(), default);
        }
        await using (var replay = fixture.Context()) await fixture.Orders(replay).CancelAsync(1, false, ids[0], new(), default);
        var second = await fixture.Quote(1);
        int orderId;
        await using (var next = fixture.Context()) orderId = (await fixture.Orders(next).PlaceAsync(1, second, default)).Id;
        foreach (var state in new[] { OrderStatus.CONFIRMED, OrderStatus.SHIPPING, OrderStatus.DELIVERED, OrderStatus.DELIVERED })
        {
            await using var change = fixture.Context();
            await fixture.Orders(change).TransitionAsync(3, orderId, new() { Status = state, ConfirmCodCollected = true }, default);
        }
        await using var verify = fixture.Context();
        var stock = await verify.Inventories.SingleAsync(); Assert.Equal(0, stock.Quantity); Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(2, await verify.Orders.CountAsync()); Assert.Equal(4, await verify.InventoryTransactions.CountAsync());
        Assert.Equal(6, await verify.OrderEvents.CountAsync());
    }

    [SqlServerFact]
    public async Task FailureWhileWritingHistory_RollsBackOrderCartAndReservation()
    {
        await using var fixture = new Database(); await fixture.Seed(); var quote = await fixture.Quote(1);
        await using (var fail = fixture.Context())
        {
            await fail.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailOrderHistory ON InventoryTransactions AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Type] = 'ORDER_PLACED') THROW 51000, 'Deliberate isolated test failure', 1; END");
            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Orders(fail).PlaceAsync(1, quote, default));
        }
        await using var verify = fixture.Context();
        Assert.Empty(await verify.Orders.ToListAsync()); Assert.Empty(await verify.OrderItems.ToListAsync());
        Assert.Empty(await verify.OrderEvents.ToListAsync()); Assert.Empty(await verify.InventoryTransactions.ToListAsync());
        Assert.Single(await verify.CartItems.ToListAsync()); Assert.Equal(0, (await verify.Inventories.SingleAsync()).ReservedQuantity);
    }

    [SqlServerFact]
    public async Task ReportQueries_TranslateOnSqlServer_UseVietnamDateBoundary()
    {
        await using var fixture = new Database(); await fixture.Seed(); var quote = await fixture.Quote(1);
        await using (var db = fixture.Context())
        {
            var service = fixture.Orders(db); var order = await service.PlaceAsync(1, quote, default);
            foreach (var state in new[] { OrderStatus.CONFIRMED, OrderStatus.SHIPPING, OrderStatus.DELIVERED })
                await service.TransitionAsync(3, order.Id, new() { Status = state, ConfirmCodCollected = true }, default);
            (await db.Orders.FindAsync(order.Id))!.DeliveredAt = new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }
        await using var reportDb = fixture.Context();
        var community = new CommunityService(reportDb, fixture.Readiness, null!);
        var result = await community.RevenueAsync(new() { FromDate = new(2026, 10, 28), ToDate = new(2026, 10, 29) }, default);
        Assert.Equal(100000, result.TotalRevenue); Assert.Equal(1, result.DeliveredOrders);
        Assert.Equal(100000, result.Days[0].Revenue); Assert.Equal(0, result.Days[1].Revenue);
        Assert.Single(result.BestSellers); Assert.Equal(1, result.BestSellers[0].Quantity);
    }

    [SqlServerFact]
    public async Task DeliveryHistoryFailure_RollsBackPaymentStatusInventoryAndTimeline()
    {
        await using var fixture = new Database(); await fixture.Seed(); var quote = await fixture.Quote(1);
        int id;
        await using (var setup = fixture.Context())
        {
            var service = fixture.Orders(setup); id = (await service.PlaceAsync(1, quote, default)).Id;
            await service.TransitionAsync(3, id, new() { Status = OrderStatus.CONFIRMED }, default);
            await service.TransitionAsync(3, id, new() { Status = OrderStatus.SHIPPING }, default);
            await setup.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailDeliveryHistory ON InventoryTransactions AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Type] = 'ORDER_DELIVERED') THROW 51000, 'Deliberate isolated delivery test failure', 1; END");
        }
        await using (var fail = fixture.Context())
            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Orders(fail).TransitionAsync(3, id,
                new() { Status = OrderStatus.DELIVERED, ConfirmCodCollected = true }, default));
        await using var verify = fixture.Context();
        var order = await verify.Orders.SingleAsync();
        Assert.Equal(OrderStatus.SHIPPING, order.Status); Assert.Equal(PaymentStatus.UNPAID, order.PaymentStatus);
        Assert.Null(order.DeliveredAt);
        var stock = await verify.Inventories.SingleAsync(); Assert.Equal(1, stock.Quantity); Assert.Equal(1, stock.ReservedQuantity);
        Assert.Equal(3, await verify.OrderEvents.CountAsync()); Assert.Single(await verify.InventoryTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task CancellationHistoryFailure_RollsBackStatusAndReservationRelease()
    {
        await using var fixture = new Database(); await fixture.Seed(); var quote = await fixture.Quote(1);
        int id;
        await using (var setup = fixture.Context())
        {
            id = (await fixture.Orders(setup).PlaceAsync(1, quote, default)).Id;
            await setup.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailCancellationHistory ON InventoryTransactions AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Type] = 'ORDER_CANCELLED') THROW 51000, 'Deliberate isolated cancellation test failure', 1; END");
        }
        await using (var fail = fixture.Context())
            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Orders(fail).CancelAsync(1, false, id, new(), default));
        await using var verify = fixture.Context();
        var order = await verify.Orders.SingleAsync(); Assert.Equal(OrderStatus.PENDING, order.Status); Assert.Null(order.CancelledAt);
        var stock = await verify.Inventories.SingleAsync(); Assert.Equal(1, stock.Quantity); Assert.Equal(1, stock.ReservedQuantity);
        Assert.Single(await verify.OrderEvents.ToListAsync()); Assert.Single(await verify.InventoryTransactions.ToListAsync());
    }
}
