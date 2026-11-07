using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("admin/sach")]
public sealed class AdminBooksController(IAdminCatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] BookQuery query, string status = "all", CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            var empty = await catalog.BookFormAsync(null, ct);
            return View(new AdminBookListViewModel(query, status, new([], 1, Math.Clamp(query.PageSize, 1, 100), 0), empty.Categories));
        }
        return View(await catalog.BooksAsync(query, status, ct));
    }

    [HttpGet("them")]
    public async Task<IActionResult> Create(CancellationToken ct) => View("Form", await catalog.BookFormAsync(null, ct));
    [HttpGet("{id:int}/sua")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", await catalog.BookFormAsync(id, ct));

    [HttpPost("them"), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public Task<IActionResult> Create(BookFormViewModel model, CancellationToken ct) => Save(null, model, ct);
    [HttpPost("{id:int}/sua"), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public Task<IActionResult> Edit(int id, BookFormViewModel model, CancellationToken ct) => Save(id, model, ct);

    [HttpPost("{id:int}/ngung-ban")]
    public Task<IActionResult> Stop(int id, CancellationToken ct) => SetStatus(id, true,
        ValidationText.Pick("Đã ngừng bán sách; tồn kho và lịch sử được giữ nguyên.", "The book is no longer for sale; stock and history are preserved."), ct);
    [HttpPost("{id:int}/khoi-phuc")]
    public Task<IActionResult> Restore(int id, CancellationToken ct) => SetStatus(id, false,
        ValidationText.Pick("Đã khôi phục sách đang bán.", "The book is back on sale."), ct);

    // Lỗi nghiệp vụ (đã ở trạng thái đích, thể loại ngừng dùng) hiển thị trên danh sách; 404/5xx vẫn đi qua middleware lỗi chung.
    private async Task<IActionResult> SetStatus(int id, bool deleted, string success, CancellationToken ct)
    {
        try
        {
            await catalog.SetBookDeletedAsync(id, deleted, ct);
            TempData["Success"] = success;
        }
        catch (AppException ex) when (ex.StatusCode == 409) { TempData["Error"] = $"{ex.Message} ({ex.Error.Code})"; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Save(int? id, BookFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid)
            try
            {
                await catalog.SaveBookAsync(id, model, model.CoverFile, ct);
                TempData["Success"] = ValidationText.Pick("Đã lưu thông tin sách. Tồn kho chỉ thay đổi qua chức năng kho và đơn hàng.", "Book details saved. Stock changes only through inventory and order operations.");
                return RedirectToAction(nameof(Index));
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409 or 422 || ex.Error.Code == "COVER_STORAGE_UNAVAILABLE") { AdminErrors.Apply(this, ex); }
        else Response.StatusCode = 400;
        var current = await catalog.BookFormAsync(id, ct);
        model.Categories = current.Categories;
        model.ExistingCoverImage = current.ExistingCoverImage;
        return View("Form", model);
    }
}
