using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Roles = RoleNames.Admin), Route("admin/the-loai")]
public sealed class AdminCategoriesController(IAdminCatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminPageQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(new CategoryListViewModel(query, await catalog.CategoriesAsync(query, ct)));
        Response.StatusCode = 400;
        return View(new CategoryListViewModel(query, new([], 1, Math.Clamp(query.PageSize, 1, 100), 0)));
    }
    [HttpGet("them")]
    public IActionResult Create() => View("Form", new CategoryFormViewModel(null, new()));
    [HttpGet("{id:int}/sua")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", new CategoryFormViewModel(id, await catalog.CategoryAsync(id, ct)));
    [HttpPost("them")]
    public Task<IActionResult> Create([Bind(Prefix = "Input")] CategoryInput input, CancellationToken ct) => Save(null, input, ct);
    [HttpPost("{id:int}/sua")]
    public Task<IActionResult> Edit(int id, [Bind(Prefix = "Input")] CategoryInput input, CancellationToken ct) => Save(id, input, ct);
    [HttpPost("{id:int}/xoa")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await catalog.DeleteCategoryAsync(id, ct);
            TempData["Success"] = ValidationText.Pick("Đã xóa mềm thể loại không có sách tham chiếu.", "The unreferenced category has been soft-deleted.");
        }
        catch (AppException ex) when (ex.StatusCode == 409)
        {
            TempData["Error"] = $"{ex.Message} ({ex.Error.Code})";
        }
        return RedirectToAction(nameof(Index));
    }
    private async Task<IActionResult> Save(int? id, CategoryInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await catalog.SaveCategoryAsync(id, input, ct);
                TempData["Success"] = ValidationText.Pick("Đã lưu thể loại sách.", "The category has been saved.");
                return RedirectToAction(nameof(Index));
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex, "Input."); }
        else Response.StatusCode = 400;
        return View("Form", new CategoryFormViewModel(id, input));
    }
}
