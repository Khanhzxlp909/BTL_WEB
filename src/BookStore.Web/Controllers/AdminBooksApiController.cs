using BookStore.Web.Domain;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[ApiController, Authorize(Roles = RoleNames.Admin), Route("api/admin/books")]
public sealed class AdminBooksApiController(IAdminCatalogService catalog) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookInput input, CancellationToken ct)
    {
        var saved = await catalog.SaveBookAsync(null, input, null, ct);
        return Created($"/api/books/{saved.Id}", saved);
    }
    [HttpPut("{id:int}")]
    public async Task<BookSavedDto> Update(int id, [FromBody] BookInput input, CancellationToken ct)
        => await catalog.SaveBookAsync(id, input, null, ct);
    [HttpDelete("{id:int}")]
    public async Task<BookSavedDto> Delete(int id, CancellationToken ct) => await catalog.SetBookDeletedAsync(id, true, ct);
}
