using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[ApiController]
public sealed class CatalogApiController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("/api/books")]
    public async Task<PagedResult<BookCardDto>> Books([FromQuery] BookQuery query, CancellationToken ct)
        => await catalog.SearchAsync(query, ct);
    [HttpGet("/api/books/{id:int}")]
    public async Task<BookDetailDto> Book(int id, CancellationToken ct) => await catalog.DetailAsync(id, ct);
    [HttpGet("/api/categories")]
    public async Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => await catalog.CategoriesAsync(ct);
}
