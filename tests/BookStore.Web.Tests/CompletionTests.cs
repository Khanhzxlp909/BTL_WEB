using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.Web.Data;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Web.Tests;

public sealed class CompletionTests
{
    [Fact]
    public async Task RestoreActiveBook_ReportsCurrentStateAndPreservesStock()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IAdminCatalogService>();
        var error = await Assert.ThrowsAsync<BookStore.Web.Infrastructure.AppException>(
            () => catalog.SetBookDeletedAsync(1, false, default));
        Assert.Equal("BOOK_NOT_STOPPED", error.Error.Code);
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.False((await db.Books.AsNoTracking().SingleAsync(x => x.Id == 1)).IsDeleted);
        Assert.Equal(10, (await db.Inventories.AsNoTracking().SingleAsync(x => x.BookId == 1)).Quantity);
    }

    [Fact]
    public async Task OrderCodeRoutes_EnforceOwnership_AndCancelOnlyOnce()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var customer = factory.Browser(); await CommerceTests.Authenticate(customer);
        var id = await CommerceTests.Place(customer, 1);
        var order = JsonDocument.Parse(await customer.GetStringAsync($"/api/orders/{id}")).RootElement;
        var code = order.GetProperty("orderCode").GetString();
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/orders/{code}")).StatusCode);
        using var admin = factory.Browser(); await CommerceTests.Authenticate(admin, "manager");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/orders/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/orders/{code}/cancellation", new {})).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync($"/api/orders/{code}/cancellation", new {})).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync($"/api/orders/{code}/cancellation", new {})).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        Assert.Equal(2, await db.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task CheckoutSuccess_IsReadOnlyAndPrivate_ContainsOrderAndNextSteps()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var customer = factory.Browser(); await CommerceTests.Authenticate(customer);
        var id = await CommerceTests.Place(customer, 1);
        var page = await customer.GetAsync($"/dat-hang/thanh-cong/{id}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("COD", html); Assert.Contains($"/don-hang/{id}", html); Assert.Contains("/sach", html);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/dat-hang/thanh-cong/{id}")).StatusCode);
        using var other = factory.Browser(); await CommerceTests.Authenticate(other, "manager");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/dat-hang/thanh-cong/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookStoreDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task AdminOrderCustomerFilter_MatchesRecipientName()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var customer = factory.Browser(); await CommerceTests.Authenticate(customer);
        await CommerceTests.Place(customer, 1);
        using var scope = factory.Services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var result = await orders.ListAsync(2, true, new() { Customer = "Người nhận thử" }, default);
        Assert.Single(result.Orders.Items);
        Assert.Empty((await orders.ListAsync(2, true, new() { Customer = "Không có người này" }, default)).Orders.Items);
    }
}
