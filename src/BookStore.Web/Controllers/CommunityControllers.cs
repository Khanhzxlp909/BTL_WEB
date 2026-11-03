using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Roles = RoleNames.Customer)]
public sealed class ReviewsController(ICommunityService community) : Controller
{
    private void RequireCustomerOnly()
    {
        if (User.IsInRole(RoleNames.Admin)) throw new AppException(403, "ADMIN_REVIEW_FORBIDDEN", ValidationText.Pick(
            "Tài khoản Admin chỉ kiểm duyệt, không được viết đánh giá. Dùng tài khoản Customer đã nhận sách.",
            "Admin accounts moderate reviews but cannot write them. Use a Customer account with a delivered purchase."));
    }
    [HttpGet("/sach/{bookId:int}/danh-gia")]
    public async Task<IActionResult> Form(int bookId, CancellationToken ct)
    { RequireCustomerOnly(); return View(await community.ReviewFormAsync(CommerceSupport.UserId(User), bookId, ct)); }
    [HttpPost("/sach/{bookId:int}/danh-gia")]
    public async Task<IActionResult> Form(int bookId, [Bind(Prefix = "Input")] ReviewInput input, CancellationToken ct)
    {
        RequireCustomerOnly();
        if (ModelState.IsValid)
        {
            var hidden = await community.SaveReviewAsync(CommerceSupport.UserId(User), bookId, input, ct);
            TempData["Success"] = hidden ? ValidationText.Pick("Đã lưu đánh giá; nội dung vẫn ẩn, chờ Admin xem xét.", "Review saved; it remains hidden pending administrator review.")
                : ValidationText.Pick("Đã lưu đánh giá của bạn.", "Your review has been saved.");
            return Redirect($"/sach/{bookId}/danh-gia");
        }
        Response.StatusCode = 400;
        var current = await community.ReviewFormAsync(CommerceSupport.UserId(User), bookId, ct);
        return View(current with { Input = input });
    }
}

[Authorize(Roles = RoleNames.Admin), Route("admin/danh-gia")]
public sealed class AdminReviewsController(ICommunityService community) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ReviewQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await community.ReviewsAsync(query, ct));
        Response.StatusCode = 400; return View(new ReviewListViewModel(query, new([], 1, 12, 0)));
    }
    [HttpPost("{id:int}/an")] public Task<IActionResult> Hide(int id, CancellationToken ct) => Moderate(id, true, ct);
    [HttpPost("{id:int}/hien")] public Task<IActionResult> Show(int id, CancellationToken ct) => Moderate(id, false, ct);
    private async Task<IActionResult> Moderate(int id, bool hidden, CancellationToken ct)
    {
        await community.ModerateAsync(CommerceSupport.UserId(User), id, hidden, ct);
        TempData["Success"] = ValidationText.Pick("Đã cập nhật trạng thái hiển thị đánh giá; nội dung được giữ nguyên.", "Review visibility updated; its content is preserved.");
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = RoleNames.Admin), Route("admin/khach-hang")]
public sealed class CustomersController(ICommunityService community) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CustomerQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await community.CustomersAsync(query, ct));
        Response.StatusCode = 400; return View(new CustomerListViewModel(query, new([], 1, 12, 0)));
    }
    [HttpGet("{id:int}/khoa")]
    public async Task<IActionResult> Lock(int id, CancellationToken ct) => View(new LockCustomerViewModel(await community.CustomerAsync(id, ct), new()));
    [HttpPost("{id:int}/khoa")]
    public async Task<IActionResult> Lock(int id, [Bind(Prefix = "Input")] LockCustomerInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            await community.LockAsync(CommerceSupport.UserId(User), id, input, ct);
            TempData["Success"] = ValidationText.Pick("Đã khóa khách hàng; các phiên cũ hết hiệu lực ở lần truy cập tiếp theo.", "Customer locked; existing sessions are invalid on their next request.");
            return RedirectToAction(nameof(Index));
        }
        Response.StatusCode = 400;
        return View(new LockCustomerViewModel(await community.CustomerAsync(id, ct), input));
    }
    [HttpPost("{id:int}/mo-khoa")]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct)
    {
        await community.UnlockAsync(id, ct);
        TempData["Success"] = ValidationText.Pick("Đã mở khóa. Khách hàng có thể đăng nhập lại.", "Customer unlocked. They can sign in again.");
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = RoleNames.Admin)]
public sealed class ReportsController(ICommunityService community) : Controller
{
    [HttpGet("/admin/bao-cao")]
    public async Task<IActionResult> Index([FromQuery] ReportQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await community.RevenueAsync(query, ct));
        Response.StatusCode = 400; return View(new RevenueViewModel(query, 0, 0, 0, [], []));
    }
}
