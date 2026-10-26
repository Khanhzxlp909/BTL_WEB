using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public sealed class HomeController(ICatalogService catalog, DatabaseReadiness readiness) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!readiness.IsConfigured) return View(new HomeViewModel(false, [], []));
        var categories = await catalog.CategoriesAsync(ct);
        var books = await catalog.SearchAsync(new BookQuery { PageSize = 8 }, ct);
        return View(new HomeViewModel(true, categories, books.Items));
    }
}
