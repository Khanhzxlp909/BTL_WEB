using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;

namespace BookStore.Web.Tests;

public sealed class AdminTests
{
    private static Dictionary<string, string> BookForm(string sku = "NEW-001") => new()
    {
        ["Title"] = "Sách quản trị", ["Author"] = "Tác giả", ["SKU"] = sku, ["CategoryId"] = "1",
        ["Price"] = "150000", ["DiscountPrice"] = "120000", ["PublishedYear"] = "2026"
    };
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> form, string? tokenPath = null)
    {
        form["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, tokenPath ?? path);
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }
    private static async Task<string> CsrfHeader(HttpClient client)
    {
        var token = await BookStoreFactory.TokenAsync(client, "/admin/sach/them");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        return token;
    }

    [Theory]
    [InlineData("/admin/sach")]
    [InlineData("/admin/the-loai")]
    [InlineData("/admin/kho")]
    [InlineData("/api/admin/books")]
    public async Task AdminRoutes_DenyCustomer(string path)
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client);
        var response = path.StartsWith("/api") ? await client.PostAsJsonAsync(path, new {}) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookCrud_CreatesZeroInventory_RejectsStockOverposting_SoftDeletesRestores()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var form = BookForm();
        form["Quantity"] = "500"; form["IsDeleted"] = "true"; form["CoverImage"] = "https://invalid.test/image";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/admin/sach/them", form)).StatusCode);
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var book = await db.Books.Include(x => x.Inventory).SingleAsync(x => x.SKU == "NEW-001");
            id = book.Id;
            Assert.False(book.IsDeleted); Assert.Null(book.CoverImage); Assert.Equal(0, book.Inventory.Quantity);
        }
        form["Price"] = "160000";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, $"/admin/sach/{id}/sua", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, $"/admin/sach/{id}/ngung-ban", new(), "/admin/sach")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/books/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, $"/admin/sach/{id}/khoi-phuc", new(), "/admin/sach")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/books/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("Price", "0")]
    [InlineData("Price", "12.5")]
    [InlineData("DiscountPrice", "150001")]
    [InlineData("PublishedYear", "9999")]
    [InlineData("Title", "   ")]
    [InlineData("CategoryId", "999")]
    public async Task BookForm_InvalidData_DoesNotCreateBook(string field, string value)
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var form = BookForm(); form[field] = value;
        var result = await Post(client, "/admin/sach/them", form);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<BookStoreDbContext>().Books.CountAsync());
    }

    [Fact]
    public async Task Categories_RejectDuplicateAndReferencedDeletion_ReserveDeletedNames()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var referenced = await Post(client, "/admin/the-loai/1/xoa", new(), "/admin/the-loai");
        Assert.Equal(HttpStatusCode.Conflict, referenced.StatusCode);
        Assert.Contains("CATEGORY_HAS_BOOKS", await referenced.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/admin/the-loai/them", new() { ["Input.Name"] = "Văn học" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/admin/the-loai/them", new() { ["Input.Name"] = "văn học" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/admin/the-loai/2/xoa", new(), "/admin/the-loai")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/admin/the-loai/them", new() { ["Input.Name"] = "Văn học" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.True((await db.Categories.FindAsync(2))!.IsDeleted);
        Assert.Equal(2, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task AdminApi_RequiresCsrf_ValidatesSku_UpdatesAndSoftDeletes()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var input = new BookInput { Title = "API book", Author = "API author", SKU = "API-001", CategoryId = 1, Price = 50000 };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/books", input)).StatusCode);
        await CsrfHeader(client);
        var response = await client.PostAsJsonAsync("/api/admin/books", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/books", input)).StatusCode);
        input.Price = 60000;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/admin/books/{id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/admin/books/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.True((await db.Books.FindAsync(id))!.IsDeleted);
        Assert.Equal(0, (await db.Inventories.SingleAsync(x => x.BookId == id)).Quantity);
    }

    [Fact]
    public async Task Import_RecordsBeforeAfter_PreservesReserved_RejectsStaleVersion()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IInventoryService>().SetThresholdAsync(new(), default);
        var html = await client.GetStringAsync("/admin/kho/1/nhap");
        var version = WebUtility.HtmlDecode(Regex.Match(html, "name=\"Input.RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(version);
        var form = new Dictionary<string, string> { ["Input.Quantity"] = "4", ["Input.Reference"] = "PN-001", ["Input.Reason"] = "Nhập thử", ["Input.RowVersion"] = version };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/admin/kho/1/nhap", form)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var stock = await db.Inventories.SingleAsync(x => x.BookId == 1);
            Assert.Equal(14, stock.Quantity); Assert.Equal(3, stock.ReservedQuantity);
            var history = await db.InventoryTransactions.SingleAsync();
            Assert.Equal(10, history.QuantityBefore); Assert.Equal(14, history.QuantityAfter);
            Assert.Equal(3, history.ReservedBefore); Assert.Equal(3, history.ReservedAfter);
            // Giả lập rowversion mới của SQL Server; SQLite không tự tăng rowversion.
            await db.Database.ExecuteSqlRawAsync("UPDATE Inventories SET RowVersion = randomblob(8) WHERE BookId = 1");
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/admin/kho/1/nhap", form)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/kho/1/lich-su")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/kho?LowStockOnly=true")).StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<BookStoreDbContext>().InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task Import_InvalidActor_RollsBackStockAndHistory()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var stock = await service.GetAsync(1, default);
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ImportAsync(1, new()
            { Quantity = 5, Reference = "ROLLBACK", Reason = "Thử rollback", RowVersion = stock.RowVersion }, 99999, default));
        }
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.Equal(10, (await db.Inventories.SingleAsync(x => x.BookId == 1)).Quantity);
        Assert.Empty(await db.InventoryTransactions.ToListAsync());
    }

    [Theory]
    [InlineData("Input.Quantity", "0")]
    [InlineData("Input.Quantity", "-1")]
    [InlineData("Input.Quantity", "1.5")]
    [InlineData("Input.Reference", " ")]
    [InlineData("Input.Reason", "")]
    [InlineData("Input.RowVersion", "invalid-version")]
    public async Task Import_InvalidInput_PreservesStockAndHistory(string field, string value)
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        using var scope = factory.Services.CreateScope();
        var version = (await scope.ServiceProvider.GetRequiredService<IInventoryService>().GetAsync(1, default)).RowVersion;
        var form = new Dictionary<string, string>
        { ["Input.Quantity"] = "1", ["Input.Reference"] = "PN-INVALID", ["Input.Reason"] = "Thử kiểm tra", ["Input.RowVersion"] = version };
        form[field] = value;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/admin/kho/1/nhap", form)).StatusCode);
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.Equal(10, (await db.Inventories.AsNoTracking().SingleAsync(x => x.BookId == 1)).Quantity);
        Assert.Empty(await db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task ThresholdChange_RecalculatesLowStockWithoutChangingQuantities()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        await inventory.SetThresholdAsync(new() { Value = 5 }, default);
        var before = await inventory.ListAsync(new() { LowStockOnly = true }, default);
        Assert.DoesNotContain(before.Inventories.Items, x => x.BookId == 1);
        await inventory.SetThresholdAsync(new() { Value = 7 }, default);
        var after = await inventory.ListAsync(new() { LowStockOnly = true }, default);
        Assert.Contains(after.Inventories.Items, x => x.BookId == 1 && x.AvailableQuantity == 7);
        Assert.Equal(10, (await inventory.GetAsync(1, default)).Quantity);
    }

    [Fact]
    public async Task Threshold_InvalidNegativeDoesNotChangeExistingValue()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IInventoryService>().SetThresholdAsync(new() { Value = 5 }, default);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/admin/kho/nguong", new() { ["Value"] = "-1" })).StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.Equal(5, await verify.ServiceProvider.GetRequiredService<IInventoryService>().ThresholdAsync(default));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, ".png")]
    [InlineData(SKEncodedImageFormat.Jpeg, ".jpg")]
    [InlineData(SKEncodedImageFormat.Webp, ".webp")]
    public async Task Cover_DecodesAndReencodes_GeneratesSafeName(SKEncodedImageFormat format, string extension)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bookstore-cover-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var storage = new CoverImageStorage(new TestEnvironment(directory));
            using var bitmap = new SKBitmap(4, 4); bitmap.Erase(SKColors.ForestGreen);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(format, 90);
            await using var stream = new MemoryStream(encoded.ToArray());
            var path = await storage.SaveAsync(new FormFile(stream, 0, stream.Length, "CoverFile", $"../../unsafe{extension}"), default);
            Assert.Matches("^/uploads/covers/[a-f0-9]{32}\\.webp$", path);
            Assert.True(File.Exists(Path.Combine(directory, path.TrimStart('/'))));
            await storage.DeleteAsync(path, default);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Cover_RejectsFakeImageAndOversize_DoesNotCreateBook()
    {
        using var factory = new BookStoreFactory();
        await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        using var content = new MultipartFormDataContent();
        foreach (var (key, value) in BookForm()) content.Add(new StringContent(value), key);
        content.Add(new StringContent(await BookStoreFactory.TokenAsync(client, "/admin/sach/them")), "__RequestVerificationToken");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<script>not an image</script>")), "CoverFile", "cover.png");
        var response = await client.PostAsync("/admin/sach/them", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("COVER_IMAGE_INVALID", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<BookStoreDbContext>().Books.CountAsync());
        var storage = scope.ServiceProvider.GetRequiredService<ICoverImageStorage>();
        await using var oversized = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        var error = await Assert.ThrowsAsync<AppException>(() => storage.SaveAsync(new FormFile(oversized, 0, oversized.Length, "CoverFile", "large.png"), default));
        Assert.Equal("COVER_IMAGE_INVALID", error.Error.Code);
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BookStore.Web.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = root;
        public string ContentRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
