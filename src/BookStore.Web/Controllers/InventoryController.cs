using System.Security.Claims;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Roles = RoleNames.Admin), Route("admin/kho")]
public sealed class InventoryController(IInventoryService inventory) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] InventoryQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await inventory.ListAsync(query, ct));
        Response.StatusCode = 400;
        return View(new InventoryListViewModel(query, new([], 1, Math.Clamp(query.PageSize, 1, 100), 0), await inventory.ThresholdAsync(ct)));
    }
    [HttpGet("{bookId:int}/nhap")]
    public async Task<IActionResult> Import(int bookId, CancellationToken ct)
    {
        var item = await inventory.GetAsync(bookId, ct);
        return View(new ImportViewModel(item, new() { RowVersion = item.RowVersion }));
    }
    [HttpPost("{bookId:int}/nhap")]
    public async Task<IActionResult> Import(int bookId, [Bind(Prefix = "Input")] ImportInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await inventory.ImportAsync(bookId, input, int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
                TempData["Success"] = ValidationText.Pick("Đã nhập kho và ghi lịch sử; số giữ chỗ không thay đổi.", "Stock has been imported and recorded; reserved stock is unchanged.");
                return RedirectToAction(nameof(History), new { bookId });
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex, "Input."); }
        else Response.StatusCode = 400;
        return View(new ImportViewModel(await inventory.GetAsync(bookId, ct), input));
    }
    [HttpGet("{bookId:int}/lich-su")]
    public async Task<IActionResult> History(int bookId, [FromQuery] AdminPageQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await inventory.HistoryAsync(bookId, query, ct));
        Response.StatusCode = 400;
        return View(new InventoryHistoryViewModel(await inventory.GetAsync(bookId, ct), query, new([], 1, Math.Clamp(query.PageSize, 1, 100), 0)));
    }
    [HttpGet("nguong")]
    public async Task<IActionResult> Threshold(CancellationToken ct)
    {
        try { return View(new ThresholdInput { Value = await inventory.ThresholdAsync(ct) }); }
        catch (AppException ex) when (ex.Error.Code == "LOW_STOCK_THRESHOLD_NOT_INITIALIZED")
        {
            AdminErrors.Apply(this, ex);
            return View(new ThresholdInput());
        }
    }
    [HttpPost("nguong")]
    public async Task<IActionResult> Threshold(ThresholdInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await inventory.SetThresholdAsync(input, ct);
                TempData["Success"] = ValidationText.Pick("Đã lưu ngưỡng tồn thấp chung cho toàn bộ sách.", "The common low-stock threshold has been saved for all books.");
                return RedirectToAction(nameof(Index));
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex); }
        else Response.StatusCode = 400;
        return View(input);
    }
}
