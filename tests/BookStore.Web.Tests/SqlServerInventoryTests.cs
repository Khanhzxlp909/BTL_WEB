using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BookStore.Web.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKSTORE_TEST_SQLSERVER")))
            Skip = "Chỉ chạy với SQL Server thử nghiệm tách biệt qua Docker Compose, không dùng DB ứng dụng.";
    }
}

public sealed class SqlServerInventoryTests
{
    [SqlServerFact]
    public async Task ConcurrentImports_OnlyOneSucceeds_RollbackPreservesHistory_OnRealRowversion()
    {
        // Luôn tạo DB mới có tên ngẫu nhiên. Không đọc/ghi DB BookStore hay DB do người dùng đặt.
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BOOKSTORE_TEST_SQLSERVER"))
        { InitialCatalog = $"BookStoreTest_{Guid.NewGuid():N}" };
        var options = new DbContextOptionsBuilder<BookStoreDbContext>().UseSqlServer(builder.ConnectionString).Options;
        var readiness = new DatabaseReadiness(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:BookStore"] = "IsolatedSqlTest" }).Build());
        await using var setup = new BookStoreDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var actor = new ApplicationUser { UserName = "sql_test_actor", NormalizedUserName = "SQL_TEST_ACTOR",
                Email = "sql_test@example.test", NormalizedEmail = "SQL_TEST@EXAMPLE.TEST", PasswordHash = "test-only-not-a-login" };
            setup.Users.Add(actor);
            var book = new Book { Title = "SQL rowversion test", Author = "Test", SKU = "SQL-TEST", Price = 100000,
                Category = new Category { Name = "SQL Test" }, Inventory = new Inventory { Quantity = 5, ReservedQuantity = 2 } };
            setup.Books.Add(book);
            await setup.SaveChangesAsync();
            var original = Convert.ToBase64String(book.Inventory.RowVersion);
            async Task<string> Import()
            {
                await using var db = new BookStoreDbContext(options);
                try
                {
                    await new InventoryService(db, readiness).ImportAsync(book.Id, new ImportInput
                    { Quantity = 3, Reference = "SQL-PN", Reason = "Concurrent test", RowVersion = original }, actor.Id, default);
                    return "saved";
                }
                catch (AppException ex) { return ex.Error.Code; }
            }
            var results = await Task.WhenAll(Import(), Import());
            Assert.Single(results, x => x == "saved");
            Assert.Single(results, x => x == "INVENTORY_CONCURRENCY_CONFLICT");
            await using (var verify = new BookStoreDbContext(options))
            {
                var stock = await verify.Inventories.SingleAsync();
                Assert.Equal(8, stock.Quantity); Assert.Equal(2, stock.ReservedQuantity);
                Assert.NotEqual(original, Convert.ToBase64String(stock.RowVersion));
                Assert.Single(await verify.InventoryTransactions.ToListAsync());
            }
            await using (var failing = new BookStoreDbContext(options))
            {
                var service = new InventoryService(failing, readiness);
                var current = await service.GetAsync(book.Id, default);
                await Assert.ThrowsAsync<DbUpdateException>(() => service.ImportAsync(book.Id, new ImportInput
                { Quantity = 7, Reference = "SQL-FAIL", Reason = "Rollback test", RowVersion = current.RowVersion }, int.MaxValue, default));
            }
            await using var final = new BookStoreDbContext(options);
            Assert.Equal(8, (await final.Inventories.SingleAsync()).Quantity);
            Assert.Single(await final.InventoryTransactions.ToListAsync());
        }
        finally
        {
            // Chỉ xóa đúng DB test do bài này tạo, không xóa volume hoặc DB ứng dụng.
            Assert.StartsWith("BookStoreTest_", builder.InitialCatalog);
            await setup.Database.EnsureDeletedAsync();
        }
    }
}
