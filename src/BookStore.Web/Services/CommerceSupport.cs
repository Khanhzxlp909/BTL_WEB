using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public static class CommerceSupport
{
    public const decimal MoneyLimit = 999999999999999999m;
    public static int UserId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static DateTime StartUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
    public static AppException Conflict(string code, string vi, string en) => new(409, code, ValidationText.Pick(vi, en));
    public static AppException OrderMissing(int id) => new(404, "ORDER_NOT_FOUND", ValidationText.Pick(
        $"Không tìm thấy đơn hàng #{id} thuộc phạm vi truy cập của bạn. Kiểm tra mã hoặc mở lịch sử đơn của mình.",
        $"Order #{id} was not found within your access scope. Check the ID or open your order history."));

    public static async Task LockUserAsync(BookStoreDbContext db, int userId, CancellationToken ct)
    {
        var query = db.Database.IsSqlServer()
            ? db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
            : db.Users.Where(x => x.Id == userId);
        if (!await query.AnyAsync(x => x.IsActive, ct)) throw new AppException(401, "ACCOUNT_NOT_ACTIVE", ValidationText.Pick(
            "Tài khoản không hoạt động hoặc không còn tồn tại. Đăng nhập lại; liên hệ quản trị viên nếu tài khoản bị khóa.",
            "The account is inactive or unavailable. Sign in again; contact an administrator if it is locked."));
    }

    public static async Task<Inventory> LockInventoryAsync(BookStoreDbContext db, int bookId, CancellationToken ct)
    {
        var query = db.Database.IsSqlServer()
            ? db.Inventories.FromSqlInterpolated($"SELECT * FROM [Inventories] WITH (UPDLOCK, HOLDLOCK) WHERE [BookId] = {bookId}")
            : db.Inventories.Where(x => x.BookId == bookId);
        return await query.SingleOrDefaultAsync(ct) ?? throw AdminErrors.BookMissing(bookId);
    }

    public static InventoryTransaction Movement(Inventory stock, Order order, int actor, InventoryTransactionType type,
        int quantityDelta, int reservedDelta, string? reason = null)
    {
        var entry = new InventoryTransaction
        {
            InventoryId = stock.Id, Order = order, ActorUserId = actor, Type = type,
            QuantityDelta = quantityDelta, ReservedDelta = reservedDelta,
            QuantityBefore = stock.Quantity, QuantityAfter = stock.Quantity + quantityDelta,
            ReservedBefore = stock.ReservedQuantity, ReservedAfter = stock.ReservedQuantity + reservedDelta,
            Reference = order.OrderCode, Reason = reason
        };
        stock.Quantity += quantityDelta;
        stock.ReservedQuantity += reservedDelta;
        return entry;
    }

    public static string Fingerprint(IEnumerable<CartLineDto> lines)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            lines.OrderBy(x => x.BookId).Select(x => new { x.BookId, x.Quantity, x.UnitPrice, x.Title, x.SKU })))));

    public static bool IsWriteConflict(Exception ex) => ex is DbUpdateConcurrencyException
        || ex is Microsoft.Data.SqlClient.SqlException { Number: 1205 or 1222 }
        || ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 1205 or 1222 };
    public static AppException WriteConflict() => Conflict("COMMERCE_CONCURRENCY_CONFLICT",
        "Không thể lưu thao tác mua hàng vì giỏ, đơn hoặc tồn kho vừa thay đổi bởi yêu cầu khác. Tải lại số liệu; kiểm tra lịch sử đơn trước khi gửi lại.",
        "The cart, order or inventory changed during this operation. Reload the data; check order history before submitting again.");
}

public sealed record CheckoutQuote(int UserId, string OrderCode, string Fingerprint, DateTime ExpiresAt);
public sealed class CheckoutQuotes(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("BookStore.Checkout.v1");
    public string Create(int userId, CartDto cart) => protector.Protect(JsonSerializer.Serialize(new CheckoutQuote(
        userId, $"DH{Guid.NewGuid():N}"[..30], CommerceSupport.Fingerprint(cart.Items), DateTime.UtcNow.AddDays(1))));
    public CheckoutQuote Read(int userId, string token)
    {
        try
        {
            var quote = JsonSerializer.Deserialize<CheckoutQuote>(protector.Unprotect(token));
            if (quote is null || quote.UserId != userId) throw new CryptographicException();
            return quote;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        {
            throw AdminErrors.Field("CHECKOUT_QUOTE_INVALID", "QuoteToken", ValidationText.Pick(
                "Báo giá không hợp lệ hoặc không thuộc tài khoản hiện tại. Tải lại trang đặt hàng và xác nhận báo giá mới.",
                "The quote is invalid or belongs to another account. Reload checkout and confirm the new quotation."));
        }
    }
}
