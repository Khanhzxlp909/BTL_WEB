using System.Net;
using System.Net.Http.Json;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Web.Tests;

public sealed class CommunityTests
{
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> input, string tokenPath)
    {
        input["__RequestVerificationToken"] = await BookStoreFactory.TokenAsync(client, tokenPath);
        return await client.PostAsync(path, new FormUrlEncodedContent(input));
    }
    private static async Task Deliver(HttpClient admin, int id)
    {
        foreach (var status in new[] { "CONFIRMED", "SHIPPING", "DELIVERED" })
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status, confirmCodCollected = true })).StatusCode);
    }

    [Fact]
    public async Task Review_RequiresDeliveredBook_UpdatesSameReview_StaysHiddenAfterEdit()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var reader = factory.Browser(); await CommerceTests.Authenticate(reader);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/sach/1/danh-gia")).StatusCode);
        var id = await CommerceTests.Place(reader, 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/sach/1/danh-gia")).StatusCode);
        using var admin = factory.Browser(); await CommerceTests.Authenticate(admin, "manager"); await Deliver(admin, id);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/sach/1/danh-gia")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(reader, "/sach/1/danh-gia", new() { ["Input.Rating"] = "4", ["Input.Comment"] = "Nội dung mới" }, "/sach/1/danh-gia")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(reader, "/sach/1/danh-gia", new() { ["Input.Rating"] = "6", ["Input.Comment"] = "Sai điểm" }, "/sach/1/danh-gia")).StatusCode);
        int reviewId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var review = await db.Reviews.SingleAsync(x => x.User.UserName == "reader" && x.BookId == 1);
            reviewId = review.Id; Assert.Equal(4, review.Rating); Assert.Equal("Nội dung mới", review.Comment);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/admin/danh-gia/{reviewId}/an", new(), "/admin/danh-gia")).StatusCode);
        DateTime? moderated;
        using (var scope = factory.Services.CreateScope()) moderated = (await scope.ServiceProvider.GetRequiredService<BookStoreDbContext>().Reviews.FindAsync(reviewId))!.ModeratedAt;
        await Post(admin, $"/admin/danh-gia/{reviewId}/an", new(), "/admin/danh-gia");
        await Post(reader, "/sach/1/danh-gia", new() { ["Input.Rating"] = "5", ["Input.Comment"] = "Sửa khi ẩn" }, "/sach/1/danh-gia");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            var review = await db.Reviews.FindAsync(reviewId); Assert.True(review!.IsHidden); Assert.Equal(moderated, review.ModeratedAt);
            Assert.Equal(2, await db.Reviews.CountAsync());
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/admin/danh-gia/{reviewId}/hien", new(), "/admin/danh-gia")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/sach/1/danh-gia")).StatusCode);
    }

    [Fact]
    public async Task CustomerLock_RequiresReason_ProtectsAdmin_InvalidatesSession_UnlockAllowsNewLogin()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var reader = factory.Browser(); await CommerceTests.Authenticate(reader);
        using var admin = factory.Browser(); await CommerceTests.Authenticate(admin, "manager");
        int id, adminId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            id = (await db.Users.SingleAsync(x => x.UserName == "reader")).Id;
            adminId = (await db.Users.SingleAsync(x => x.UserName == "manager")).Id;
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, $"/admin/khach-hang/{id}/khoa", new() { ["Input.Reason"] = " " }, "/admin/khach-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(admin, $"/admin/khach-hang/{adminId}/khoa", new() { ["Input.Reason"] = "Không được khóa Admin" }, "/admin/khach-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/admin/khach-hang/{id}/khoa", new() { ["Input.Reason"] = "Khóa kiểm thử" }, "/admin/khach-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/cart")).StatusCode);
        reader.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.Forbidden, (await BookStoreFactory.LoginAsync(reader)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/admin/khach-hang/{id}/mo-khoa", new(), "/admin/khach-hang")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BookStoreFactory.LoginAsync(reader)).StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<BookStoreDbContext>().Users.FindAsync(id))!.IsActive);
    }

    [Fact]
    public async Task Report_UsesVietnamDates_DeliveredAtAndCancelledAt_ZeroFillsDays()
    {
        using var factory = new BookStoreFactory(); await factory.SeedAsync();
        using var reader = factory.Browser(); await CommerceTests.Authenticate(reader);
        using var admin = factory.Browser(); await CommerceTests.Authenticate(admin, "manager");
        var first = await CommerceTests.Place(reader, 2); await Deliver(admin, first);
        var second = await CommerceTests.Place(reader, 1); await reader.PostAsJsonAsync($"/api/orders/{second}/cancel", new { });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
            (await db.Orders.FindAsync(first))!.DeliveredAt = new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc); // 28/10 00:00 VN
            (await db.Orders.FindAsync(second))!.CancelledAt = new DateTime(2026, 10, 27, 16, 59, 59, DateTimeKind.Utc); // 27/10 VN
            await db.SaveChangesAsync();
        }
        using var verify = factory.Services.CreateScope(); var community = verify.ServiceProvider.GetRequiredService<ICommunityService>();
        var result = await community.RevenueAsync(new() { FromDate = new(2026, 10, 28), ToDate = new(2026, 10, 29) }, default);
        Assert.Equal(160000, result.TotalRevenue); Assert.Equal(1, result.DeliveredOrders); Assert.Equal(0, result.CancelledOrders);
        Assert.Equal(2, result.Days.Count); Assert.Equal(0, result.Days[1].Revenue); Assert.Equal(2, result.BestSellers.Single().Quantity);
        var previousDay = await community.RevenueAsync(new() { FromDate = new(2026, 10, 27), ToDate = new(2026, 10, 27) }, default);
        Assert.Equal(0, previousDay.TotalRevenue); Assert.Equal(1, previousDay.CancelledOrders);
        var invalidRange = await admin.GetAsync("/admin/bao-cao?FromDate=2026-10-29&ToDate=2026-10-28");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        var html = await invalidRange.Content.ReadAsStringAsync();
        Assert.DoesNotContain("bs-revenue-chart", html);
        Assert.DoesNotContain("<table", html);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/admin/bao-cao")).StatusCode);
    }
}
