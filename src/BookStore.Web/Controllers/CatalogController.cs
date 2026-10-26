using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public sealed class CatalogController(ICatalogService catalog) : Controller
{
    [HttpGet("/sach")]
    public async Task<IActionResult> Index([FromQuery] BookQuery query, CancellationToken ct)
    {
        var categories = await catalog.CategoriesAsync(ct);
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return View(new CatalogViewModel(query, new([], 1, Math.Clamp(query.PageSize, 1, 100), 0), categories));
        }
        return View(new CatalogViewModel(query, await catalog.SearchAsync(query, ct), categories));
    }

    [HttpGet("/sach/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => View(await catalog.DetailAsync(id, ct));
}
