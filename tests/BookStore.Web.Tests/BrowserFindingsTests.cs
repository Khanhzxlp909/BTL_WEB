using System.Net;
using System.Net.Http.Json;
using BookStore.Web.Infrastructure;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;

namespace BookStore.Web.Tests;

// Hồi quy cho các lỗi tìm được khi kiểm thử từng tính năng trên trình duyệt (05/11/2026).
public sealed class BrowserFindingsTests
{
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> form, string tokenPath)
    {
        form["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, tokenPath);
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Login_ReturnUrlWithVietnameseQuery_RedirectsEncoded_InsteadOf500()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        var response = await Post(client, "/tai-khoan/dang-nhap", new()
        {
            ["UserName"] = "reader", ["Password"] = BookStoreFactory.Password, ["ReturnUrl"] = "/sach?Search=Tuyển tập"
        }, "/tai-khoan/dang-nhap");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sach?Search=Tuy%E1%BB%83n%20t%E1%BA%ADp", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("https://evil.test/x")]
    [InlineData("//evil.test/x")]
    [InlineData("/\\evil.test")]
    public async Task Login_ExternalReturnUrl_StaysLocal(string returnUrl)
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        var response = await Post(client, "/tai-khoan/dang-nhap", new()
        {
            ["UserName"] = "reader", ["Password"] = BookStoreFactory.Password, ["ReturnUrl"] = returnUrl
        }, "/tai-khoan/dang-nhap");
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LanguageSwitch_VietnameseQuery_RedirectsEncoded()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        var response = await Post(client, "/ngon-ngu", new() { ["culture"] = "en", ["returnUrl"] = "/sach?Search=Văn học" }, "/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/sach?Search=V%C4%83n", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RequiredFields_ShowLocalizedMessageOnly_NotDefaultEnglish()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var html = await (await Post(client, "/admin/the-loai/them", new() { ["Input.Name"] = "" }, "/admin/the-loai/them")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("field is required", html);
        Assert.Contains("Trường này bắt buộc", WebUtility.HtmlDecode(html));
        var book = WebUtility.HtmlDecode(await (await Post(client, "/admin/sach/them", new(), "/admin/sach/them")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("field is required", book);
        Assert.DoesNotContain("CategoryId phải", book);
        Assert.Contains("Hãy chọn thể loại sách", book);
    }

    [Fact]
    public async Task CoverStorage_ReadOnlyFolder_ReturnsServiceError_NotRawException()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bookstore-ro-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        // Đặt một TỆP ở vị trí thư mục "uploads" để CreateDirectory chắc chắn thất bại trên mọi OS/quyền.
        await File.WriteAllTextAsync(Path.Combine(root, "uploads"), "block");
        try
        {
            var storage = new CoverImageStorage(new TestEnvironment(root));
            using var bitmap = new SKBitmap(4, 6); bitmap.Erase(SKColors.DarkGreen);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 90);
            await using var stream = new MemoryStream(png.ToArray());
            var error = await Assert.ThrowsAsync<AppException>(() =>
                storage.SaveAsync(new FormFile(stream, 0, stream.Length, "CoverFile", "cover.png"), default));
            Assert.Equal(503, error.StatusCode);
            Assert.Equal("COVER_STORAGE_UNAVAILABLE", error.Error.Code);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StopSale_FromAdminList_PostsOnce_HidesBook_ThenRestore()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        await BookStoreFactory.LoginAsync(client, "manager");
        var stop = await Post(client, "/admin/sach/1/ngung-ban", new(), "/admin/sach");
        Assert.Equal(HttpStatusCode.Redirect, stop.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/books/1")).StatusCode);
        // Bấm lại "Ngừng bán" (vd nút Back) không văng trang lỗi: quay về danh sách kèm thông báo trạng thái.
        var again = await Post(client, "/admin/sach/1/ngung-ban", new(), "/admin/sach");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/admin/sach/1/khoi-phuc", new(), "/admin/sach")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/books/1")).StatusCode);
    }

    [Fact]
    public async Task AddToCart_AccumulatingOverStock_ExplainsInCartAndRemaining()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        await CommerceTests.Authenticate(client);
        // Sách 1: tồn 10, giữ chỗ 3 → khả dụng 7.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 5 })).StatusCode);
        var over = await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 3 });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        var message = await over.Content.ReadAsStringAsync();
        Assert.Contains("CART_STOCK_INSUFFICIENT", message);
        Assert.Contains("Giỏ đã có 5 cuốn", message); Assert.Contains("tối đa 2 cuốn", message);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 2 })).StatusCode);
        var full = await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        var cart = await client.GetFromJsonAsync<BookStore.Web.Models.CartDto>("/api/cart");
        Assert.Equal(7, cart!.Items.Single().Quantity);
    }

    [Theory]
    [InlineData("/admin/sach/them", "manager")]
    [InlineData("/admin/kho/1/nhap", "manager")]
    [InlineData("/dat-hang", "reader")]
    [InlineData("/tai-khoan/dang-nhap", null)]
    public async Task RenderedForms_HaveNoDefaultEnglishRequiredMessage(string path, string? user)
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        if (user is not null) await BookStoreFactory.LoginAsync(client, user);
        var html = await client.GetStringAsync(path);
        Assert.DoesNotContain("field is required", html);
    }

    [Theory]
    [InlineData("/sach#danh-gia", "/sach#danh-gia")]
    [InlineData("/sach/../admin", "/admin")]
    [InlineData("/sach?Search=a&b", "/sach?Search=a&b")]
    public async Task Login_ReturnUrl_KeepsFragment_NormalizesSafely(string returnUrl, string expected)
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser();
        var response = await Post(client, "/tai-khoan/dang-nhap", new()
        {
            ["UserName"] = "reader", ["Password"] = BookStoreFactory.Password, ["ReturnUrl"] = returnUrl
        }, "/tai-khoan/dang-nhap");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location!.OriginalString);
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
