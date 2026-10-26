using System.Net;
using System.Text.RegularExpressions;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookStore.Web.Tests;

public sealed class BookStoreFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public const string Password = "  test password  ";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        connection.CreateCollation("NOCASE", (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:BookStore"] = "TestDatabaseConfigured" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BookStoreDbContext>();
            services.RemoveAll<DbContextOptions<BookStoreDbContext>>();
            services.AddScoped<BookStoreDbContext>(_ => new TestDbContext(new DbContextOptionsBuilder<BookStoreDbContext>()
                .UseSqlite(connection).Options));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in new[] { RoleNames.Customer, RoleNames.Admin })
            Assert.True((await roles.CreateAsync(new IdentityRole<int>(role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (name, role) in new[] { ("reader", RoleNames.Customer), ("manager", RoleNames.Admin) })
        {
            var user = new ApplicationUser { UserName = name, Email = $"{name}@example.test" };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var category = new Category { Name = "Khoa học" };
        var book = new Book
        {
            Title = "Sách kiểm thử", Author = "Tác giả mẫu", SKU = "TEST-001", Category = category,
            Price = 100000, DiscountPrice = 80000, Description = "<script>unsafe()</script>",
            Inventory = new Inventory { Quantity = 10, ReservedQuantity = 3 }
        };
        db.Books.Add(book);
        db.Books.Add(new Book { Title = "Sách ngừng bán", Author = "Mẫu", SKU = "STOP-001", Category = category,
            Price = 50000, IsDeleted = true, Inventory = new Inventory() });
        var reader = await users.FindByNameAsync("reader");
        var admin = await users.FindByNameAsync("manager");
        db.Reviews.Add(new Review { Book = book, UserId = reader!.Id, Rating = 5, Comment = "Bài đánh giá công khai" });
        db.Reviews.Add(new Review { Book = book, UserId = admin!.Id, Rating = 1, Comment = "Nội dung bị ẩn", IsHidden = true,
            ModeratedByUserId = admin.Id, ModeratedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Thiếu mã chống giả mạo trên {path}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string name = "reader") =>
        await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TokenAsync(client, "/tai-khoan/dang-nhap"),
            ["UserName"] = name, ["Password"] = Password
        }));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    private sealed class TestDbContext(DbContextOptions<BookStoreDbContext> options) : BookStoreDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Category>().Property(x => x.Name).UseCollation("NOCASE");
            builder.Entity<Book>().Property(x => x.SKU).UseCollation("NOCASE");
            // SQLite không có SQL Server rowversion. Chỉ cấp giá trị ban đầu cho test;
            // không dùng fixture này để chứng minh cạnh tranh cập nhật SQL Server.
            builder.Entity<Inventory>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");
            builder.Entity<Order>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");
            // SQLite lưu decimal dạng TEXT; dùng INTEGER VND để phép so sánh giá giữ đúng ngữ nghĩa.
            foreach (var entity in builder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                    builder.Entity(entity.ClrType).Property(property.Name).HasConversion<long>();
        }
    }
}
