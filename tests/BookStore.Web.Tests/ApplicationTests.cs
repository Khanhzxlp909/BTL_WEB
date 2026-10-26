using System.Net;
using System.Text.Json;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public async Task Catalog_UsesSellingPrice_Availability_HidesStoppedBooksAndHiddenReviews()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        var result = JsonDocument.Parse(await client.GetStringAsync("/api/books?MinPrice=75000&MaxPrice=85000&InStock=true&Page=999"));
        var root = result.RootElement;
        Assert.Equal(1, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(7, root.GetProperty("items")[0].GetProperty("availableQuantity").GetInt32());
        var detail = JsonDocument.Parse(await client.GetStringAsync("/api/books/1")).RootElement;
        Assert.Equal(5, detail.GetProperty("averageRating").GetDouble());
        Assert.Equal(1, detail.GetProperty("reviewCount").GetInt32());
        Assert.Single(detail.GetProperty("reviews").EnumerateArray());
        Assert.EndsWith("Z", detail.GetProperty("reviews")[0].GetProperty("createdAt").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/books/2")).StatusCode);
    }

    [Theory]
    [InlineData("MinPrice=90000&MaxPrice=1", "MaxPrice")]
    [InlineData("PageSize=101", "PageSize")]
    [InlineData("Page=0", "Page")]
    [InlineData("MinPrice=12.5", "MinPrice")]
    [InlineData("Page=abc", "Page")]
    [InlineData("Sort=invalid", "Sort")]
    public async Task Catalog_InvalidFilters_ReturnFieldErrors(string query, string field)
    {
        using var factory = new BookStoreFactory();
        using var client = factory.Browser();
        var response = await client.GetAsync($"/api/books?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("VALIDATION_FAILED", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("details").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Registration_CreatesOnlyCustomer_HashesPassword_PreservesSpaces_RejectsDuplicate()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, "/tai-khoan/dang-ky"),
            ["UserName"] = "new_reader", ["Email"] = "new@example.test", ["Password"] = BookStoreFactory.Password,
            ["ConfirmPassword"] = BookStoreFactory.Password, ["Role"] = "Admin", ["IsActive"] = "false"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/tai-khoan/dang-ky", new FormUrlEncodedContent(form))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync("new_reader");
        Assert.NotNull(user);
        Assert.True(user.IsActive);
        Assert.NotEqual(BookStoreFactory.Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, BookStoreFactory.Password));
        Assert.False(await users.CheckPasswordAsync(user, BookStoreFactory.Password.Trim()));
        Assert.Equal(new[] { RoleNames.Customer }, await users.GetRolesAsync(user));
        form["UserName"] = "NEW_READER";
        var duplicate = await client.PostAsync("/tai-khoan/dang-ky", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(1, await users.Users.CountAsync(x => x.NormalizedUserName == "NEW_READER"));
        Assert.DoesNotContain($"value=\"{BookStoreFactory.Password}\"", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Forms_RejectMissingCsrf_WithStructuredJsonError()
    {
        using var factory = new BookStoreFactory();
        using var client = factory.Browser();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/tai-khoan/dang-ky")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string>()) };
        request.Headers.Accept.ParseAdd("application/json");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ANTIFORGERY_TOKEN_INVALID", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Authentication_ProtectsAdmin_RejectsOpenRedirect_UsesSameFailureMessage()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/admin")).StatusCode);
        client.DefaultRequestHeaders.Accept.Clear();
        var failedBodies = new List<string>();
        foreach (var name in new[] { "reader", "missing_reader" })
        {
            var response = await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, "/tai-khoan/dang-nhap"),
                ["UserName"] = name, ["Password"] = "incorrect password"
            }));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            failedBodies.Add(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
        Assert.All(failedBodies, body => Assert.Contains("Không thể đăng nhập: thông tin xác thực không đúng hoặc tài khoản đang bị khóa.", body));
        var login = await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, "/tai-khoan/dang-nhap"),
            ["UserName"] = "reader", ["Password"] = BookStoreFactory.Password, ["ReturnUrl"] = "https://example.test/steal"
        }));
        Assert.Equal("/", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Profile_UpdatesOnlyOwnNameAndPhone_RejectsOverposting()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client);
        var response = await client.PostAsync("/tai-khoan/ho-so", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, "/tai-khoan/ho-so"),
            ["FullName"] = "Người đọc mẫu", ["PhoneNumber"] = "0900000000", ["Email"] = "changed@example.test",
            ["UserName"] = "manager", ["Role"] = "Admin", ["Id"] = "2"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync("reader");
        Assert.Equal("Người đọc mẫu", user!.FullName);
        Assert.Equal("reader@example.test", user.Email);
        Assert.False(await users.IsInRoleAsync(user, RoleNames.Admin));
    }

    [Fact]
    public async Task ChangedPassword_ImmediatelyInvalidatesOtherSessions()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await BookStoreFactory.LoginAsync(first);
        await BookStoreFactory.LoginAsync(second);
        var response = await first.PostAsync("/tai-khoan/doi-mat-khau", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(first, "/tai-khoan/doi-mat-khau"),
            ["CurrentPassword"] = BookStoreFactory.Password, ["NewPassword"] = "a replacement password", ["ConfirmPassword"] = "a replacement password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await first.GetAsync("/tai-khoan/ho-so")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await second.GetAsync("/tai-khoan/ho-so")).StatusCode);
    }

    [Fact]
    public async Task DisabledAccount_ExistingSessionRejectedOnNextRequest()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var user = await db.Users.SingleAsync(x => x.UserName == "reader");
            user.IsActive = false;
            user.LockReason = "Kiểm thử khóa";
            user.LockedByUserId = (await db.Users.SingleAsync(x => x.UserName == "manager")).Id;
            user.LockedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/tai-khoan/ho-so")).StatusCode);
        var login = await BookStoreFactory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task LanguageCookie_PreservesLocalQuery_RejectsUnknownLanguage()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        var token = await BookStoreFactory.TokenAsync(client, "/");
        var response = await client.PostAsync("/ngon-ngu", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["culture"] = "en", ["returnUrl"] = "/sach?Search=book&Page=2" }));
        Assert.Equal("/sach?Search=book&Page=2", response.Headers.Location!.OriginalString);
        Assert.Contains(".AspNetCore.Culture", string.Join("", response.Headers.GetValues("Set-Cookie")));
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        Assert.Contains("lang=\"en\"", html);
        var invalid = await client.PostAsync("/ngon-ngu", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["culture"] = "xx" }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task BookView_EncodesUserControlledDescription()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        var html = await client.GetStringAsync("/sach/1");
        Assert.DoesNotContain("<script>unsafe()</script>", html);
        Assert.Contains("&lt;script&gt;unsafe()&lt;/script&gt;", html);
    }

    [Fact]
    public async Task AdminDashboardAndMvcCatalog_RenderWithDatabaseData()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var catalog = await client.GetAsync("/sach?Search=TEST-001&InStock=true");
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        Assert.Contains("TEST-001", await catalog.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/sach?PageSize=101")).StatusCode);
        var login = await BookStoreFactory.LoginAsync(client, "manager");
        Assert.Equal("/admin", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public void SqlServerModel_HasRestrictedForeignKeys_MoneyPrecision_ConcurrencyTokens()
    {
        using var db = new BookStoreDbContext(new DbContextOptionsBuilder<BookStoreDbContext>()
            .UseSqlServer("Server=localhost;Database=NotConnected;Integrated Security=true").Options);
        Assert.All(db.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.All(db.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)),
            p => { Assert.Equal(18, p.GetPrecision()); Assert.Equal(0, p.GetScale()); });
        Assert.True(db.Model.FindEntityType(typeof(Inventory))!.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(Order))!.FindProperty("RowVersion")!.IsConcurrencyToken);
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CK_Inventories_Quantity", sql);
        Assert.Contains("CK_Orders_Payment", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql);
    }

    [Fact]
    public async Task MissingDatabase_ShowsSetupPage_AndStructuredApiError()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:BookStore"] = "" })));
        using var client = factory.CreateClient();
        Assert.Contains("ConnectionStrings__BookStore", await client.GetStringAsync("/"));
        var response = await client.GetAsync("/api/books");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("DATABASE_NOT_CONFIGURED", await response.Content.ReadAsStringAsync());
        var missing = await client.GetAsync("/missing-page");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("RESOURCE_NOT_FOUND", await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Initializer_IsRepeatable_DoesNotPromoteExistingCustomer()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:UserName"] = "bootstrap_admin",
            ["BootstrapAdmin:Email"] = "bootstrap@example.test",
            ["BootstrapAdmin:Password"] = "test bootstrap password"
        }).Build();
        await SystemInitializer.InitializeAsync(scope.ServiceProvider, config);
        await SystemInitializer.InitializeAsync(scope.ServiceProvider, config);
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.UserName == "bootstrap_admin"));
        Assert.Equal("5", (await db.SystemSettings.SingleAsync()).SettingValue);
        config["BootstrapAdmin:UserName"] = "reader";
        var error = await Assert.ThrowsAsync<AppException>(() => SystemInitializer.InitializeAsync(scope.ServiceProvider, config));
        Assert.Equal("ADMIN_USERNAME_OCCUPIED", error.Error.Code);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.False(await users.IsInRoleAsync((await users.FindByNameAsync("reader"))!, RoleNames.Admin));
    }

    [Fact]
    public async Task History_IsAppendOnly_InventoryConstraintRejectsOverReservation()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        var inventory = await db.Inventories.SingleAsync(x => x.BookId == 1);
        inventory.ReservedQuantity = inventory.Quantity + 1;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var history = new InventoryTransaction
        {
            InventoryId = inventory.Id, ActorUserId = (await db.Users.FirstAsync()).Id,
            Type = InventoryTransactionType.IMPORT, QuantityDelta = 1, QuantityBefore = 9, QuantityAfter = 10,
            Reference = "TEST-IMPORT", Reason = "Dữ liệu kiểm thử"
        };
        db.InventoryTransactions.Add(history);
        await db.SaveChangesAsync();
        history.Reason = "Không được sửa";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.InventoryTransactions.Remove(await db.InventoryTransactions.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
