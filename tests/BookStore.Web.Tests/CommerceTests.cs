using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Web.Tests;

public sealed class CommerceTests
{
    internal static async Task Authenticate(HttpClient client, string name = "reader")
    {
        Assert.Equal(HttpStatusCode.Redirect, (await BookStoreFactory.LoginAsync(client, name)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await BookStoreFactory.TokenAsync(client, "/tai-khoan/ho-so"));
    }
    internal static async Task<CheckoutInput> Quote(HttpClient client)
    {
        var view = await client.GetFromJsonAsync<CheckoutViewModel>("/api/orders/quote");
        return new() { RecipientName = "Người nhận thử", Phone = "0900000000", ShippingAddress = "Địa chỉ kiểm thử",
            QuoteToken = view!.Input.QuoteToken, ConfirmQuote = true };
    }
    internal static async Task<int> Place(HttpClient client, int quantity = 2)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/orders", await Quote(client));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Cart_PersistsPerAccount_Accumulates_ChangesViaAjax_AndNeverReservesStock()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var client = factory.Browser(); await Authenticate(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 2 })).StatusCode);
        var result = await Json(await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 3 }));
        Assert.Equal(5, result.GetProperty("itemCount").GetInt64());
        Assert.Single(result.GetProperty("items").EnumerateArray());
        var itemId = result.GetProperty("items")[0].GetProperty("id").GetInt32();
        using var other = factory.Browser(); await Authenticate(other, "manager");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/cart/items/{itemId}", new { quantity = 1 })).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);
        using var newSession = factory.Browser(); await Authenticate(newSession);
        Assert.Equal(5, (await newSession.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/cart/items/{itemId}", new { quantity = 0 })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        var stock = await db.Inventories.SingleAsync(x => x.BookId == 1);
        Assert.Equal(10, stock.Quantity); Assert.Equal(3, stock.ReservedQuantity); Assert.Empty(await db.InventoryTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(8, HttpStatusCode.Conflict)]
    public async Task Cart_InvalidQuantity_DoesNotChangeCart(int quantity, HttpStatusCode status)
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        Assert.Equal(status, (await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);
    }

    [Fact]
    public async Task Checkout_RequotesPriceChange_RejectsTampering_AndReplaysOneConfirmationAsOneOrder()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 2 });
        var input = await Quote(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            (await db.Books.FindAsync(1))!.DiscountPrice = 90000; await db.SaveChangesAsync();
        }
        var changed = await client.PostAsJsonAsync("/api/orders", input);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("CHECKOUT_QUOTE_CHANGED", (await Json(changed)).GetProperty("code").GetString());
        Assert.True((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items.Single().PriceChanged);
        Assert.True((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items.Single().PriceChanged);
        input = await Quote(client);
        var savedToken = input.QuoteToken; input.QuoteToken = "tampered";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", input)).StatusCode);
        input.QuoteToken = savedToken;
        var created = await client.PostAsJsonAsync("/api/orders", input); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetInt32();
        var replay = await client.PostAsJsonAsync("/api/orders", input); Assert.Equal(id, (await Json(replay)).GetProperty("id").GetInt32());
        using var verify = factory.Services.CreateScope(); var dbVerify = verify.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        var order = await dbVerify.Orders.Include(x => x.Items).SingleAsync();
        Assert.Equal(180000, order.TotalAmount); Assert.Equal(0, order.ShippingFee); Assert.Equal(PaymentStatus.UNPAID, order.PaymentStatus);
        Assert.Equal(90000, order.Items.Single().UnitPrice); Assert.Equal(180000, order.Items.Single().LineTotal);
        var stock = await dbVerify.Inventories.SingleAsync(x => x.BookId == 1); Assert.Equal(10, stock.Quantity); Assert.Equal(5, stock.ReservedQuantity);
        Assert.Single(await dbVerify.InventoryTransactions.ToListAsync()); Assert.Single(await dbVerify.OrderEvents.ToListAsync());
        Assert.Empty(await dbVerify.CartItems.ToListAsync());
    }

    [Fact]
    public async Task Checkout_InvalidRecipient_OrChangedCartDoesNotPlaceOrder()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 1 }); var input = await Quote(client);
        input.Phone = "abc"; Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", input)).StatusCode);
        input.Phone = "0900000000"; input.ConfirmQuote = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", input)).StatusCode);
        input.ConfirmQuote = true; await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/orders", input)).StatusCode);
        using var scope = factory.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<BookStoreDbContext>().Orders.ToListAsync());
    }

    [Fact]
    public async Task OrderLifecycle_EnforcesSteps_CodAndSnapshot_DoesNotDeductTwice()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        var id = await Place(client);
        using var admin = factory.Browser(); await Authenticate(admin, "manager");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "SHIPPING" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "SHIPPING" })).StatusCode);
        foreach (var status in new[] { "CONFIRMED", "SHIPPING" })
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Không nhận" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "DELIVERED" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "DELIVERED", confirmCodCollected = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = "DELIVERED", confirmCodCollected = true })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var book = (await db.Books.FindAsync(1))!; book.Title = "Tên mới"; book.Price = 200000; book.IsDeleted = true; await db.SaveChangesAsync();
        }
        var order = await Json(await client.GetAsync($"/api/orders/{id}"));
        Assert.Equal("Sách kiểm thử", order.GetProperty("items")[0].GetProperty("bookTitle").GetString());
        Assert.Equal("PAID", order.GetProperty("paymentStatus").GetString());
        using var verify = factory.Services.CreateScope(); var check = verify.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        var stock = await check.Inventories.SingleAsync(x => x.BookId == 1); Assert.Equal(8, stock.Quantity); Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(4, await check.OrderEvents.CountAsync()); Assert.Equal(2, await check.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task Cancellation_ReleasesOnlyOnce_AdminReasonRequired_OwnershipEnforced()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        var id = await Place(client); using var admin = factory.Browser(); await Authenticate(admin, "manager");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/orders/{id}/cancel", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Khác chủ" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Đổi ý" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Lặp" })).StatusCode);
        using var verify = factory.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        var stock = await db.Inventories.SingleAsync(x => x.BookId == 1); Assert.Equal(10, stock.Quantity); Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(2, await db.InventoryTransactions.CountAsync()); Assert.Equal(2, await db.OrderEvents.CountAsync());
    }

    [Fact]
    public async Task MvcCommerceViews_RenderAndPreserveRecipientWhenQuoteChanges()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gio-hang")).StatusCode);
        var siteStyles = await client.GetStringAsync("/css/site.css");
        Assert.DoesNotMatch(@"\.card\s*\{\s*height\s*:\s*100%\s*;", siteStyles);
        await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 1 });
        var checkout = await client.GetStringAsync("/dat-hang");
        Assert.DoesNotContain("novalidate", checkout);
        Assert.Contains("name=\"Input.ConfirmQuote\"", checkout);
        Assert.Contains("required", checkout);
        var input = await Quote(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            (await db.Books.FindAsync(1))!.DiscountPrice = 85000; await db.SaveChangesAsync();
        }
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, "/dat-hang"),
            ["Input.RecipientName"] = input.RecipientName, ["Input.Phone"] = input.Phone,
            ["Input.ShippingAddress"] = input.ShippingAddress, ["Input.QuoteToken"] = input.QuoteToken, ["Input.ConfirmQuote"] = "true"
        };
        var changed = await client.PostAsync("/dat-hang", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var html = await changed.Content.ReadAsStringAsync();
        Assert.Contains("CHECKOUT_QUOTE_CHANGED", html); Assert.Contains(input.Phone, html);
        var newQuote = System.Text.RegularExpressions.Regex.Match(html, "name=\"Input.QuoteToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(newQuote); Assert.NotEqual(input.QuoteToken, newQuote);
        Assert.DoesNotMatch("type=\"checkbox\"[^>]*checked=\"checked\"[^>]*name=\"Input.ConfirmQuote\"", html);
        form["Input.QuoteToken"] = WebUtility.HtmlDecode(newQuote);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/dat-hang", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/don-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/don-hang/1")).StatusCode);
        using var admin = factory.Browser(); await Authenticate(admin, "manager");
        foreach (var path in new[] { "/admin/don-hang", "/admin/don-hang/1", "/admin/danh-gia", "/admin/khach-hang", "/admin/bao-cao" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CartMoneyText_PreservesWholeVndBeyondJavascriptSafeInteger()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var book = (await db.Books.FindAsync(1))!;
            book.Price = 9007199254740993m; book.DiscountPrice = null; await db.SaveChangesAsync();
        }
        using var client = factory.Browser(); await Authenticate(client);
        var cart = await Json(await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 1 }));
        Assert.Equal("9.007.199.254.740.993 đ", cart.GetProperty("items")[0].GetProperty("unitPriceText").GetString());
        Assert.Equal("9.007.199.254.740.993 đ", cart.GetProperty("totalAmountText").GetString());
    }

    [Fact]
    public async Task Checkout_InsufficientStockOrStoppedBook_PreservesCartAndReservations()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync(); using var client = factory.Browser(); await Authenticate(client);
        await client.PostAsJsonAsync("/api/cart/items", new { bookId = 1, quantity = 2 }); var quote = await Quote(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            (await db.Books.FindAsync(1))!.IsDeleted = true; await db.SaveChangesAsync();
        }
        var result = await client.PostAsJsonAsync("/api/orders", quote);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CHECKOUT_STOCK_UNAVAILABLE", (await Json(result)).GetProperty("code").GetString());
        Assert.False((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.CanCheckout);
        using var verify = factory.Services.CreateScope(); var dbVerify = verify.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.Empty(await dbVerify.Orders.ToListAsync()); Assert.Single(await dbVerify.CartItems.ToListAsync());
        Assert.Equal(3, (await dbVerify.Inventories.SingleAsync(x => x.BookId == 1)).ReservedQuantity);
    }

    [Fact]
    public async Task AdminOwnOrder_CancellationStillRequiresReason_WithoutBroadeningOwnOrderScope()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var admin = factory.Browser(); await Authenticate(admin, "manager");
        var id = await Place(admin, 1);
        var missing = await admin.PostAsJsonAsync($"/api/orders/{id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("ADMIN_CANCEL_REASON_REQUIRED", (await Json(missing)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/orders/{id}/cancel", new { reason = "Admin hủy đơn riêng" })).StatusCode);
    }
}
