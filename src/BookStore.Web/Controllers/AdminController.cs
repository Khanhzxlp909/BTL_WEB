using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminController(BookStoreDbContext db, DatabaseReadiness readiness) : Controller
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var counts = await db.Orders.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var bookCount = await db.Books.CountAsync(x => !x.IsDeleted, ct);
        var customerRole = await db.Roles.Where(x => x.Name == RoleNames.Customer).Select(x => x.Id).SingleAsync(ct);
        var customers = await db.UserRoles.CountAsync(x => x.RoleId == customerRole, ct);
        return View(new DashboardViewModel(counts.GetValueOrDefault(OrderStatus.DELIVERED),
            counts.GetValueOrDefault(OrderStatus.PENDING), counts.GetValueOrDefault(OrderStatus.CANCELLED), bookCount, customers));
    }
}
