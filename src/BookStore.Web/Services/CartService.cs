using System.Text.Json;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class CartService(BookStoreDbContext db, DatabaseReadiness readiness, IHttpContextAccessor context) : ICartService
{
    public async Task<CartDto> GetAsync(int userId, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var rows = await db.CartItems.AsNoTracking().Where(x => x.Cart.UserId == userId).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.BookId, x.Book.Title, x.Book.SKU, x.Book.CoverImage, x.Quantity,
                Price = x.Book.DiscountPrice ?? x.Book.Price, Stopped = x.Book.IsDeleted || x.Book.Category.IsDeleted,
                Available = x.Book.Inventory.Quantity - x.Book.Inventory.ReservedQuantity }).ToListAsync(ct);
        var session = context.HttpContext?.Session;
        var key = $"CartPrices:{userId}";
        var old = session?.GetString(key);
        var prices = old is null ? new Dictionary<int, decimal>() : JsonSerializer.Deserialize<Dictionary<int, decimal>>(old)!;
        var lines = rows.Select(x =>
        {
            var changed = prices.TryGetValue(x.BookId, out var price) && price != x.Price;
            var warning = x.Stopped ? ValidationText.Pick("Sách đã ngừng bán. Xóa dòng này trước khi đặt hàng.", "This book is no longer for sale. Remove it before checkout.")
                : x.Quantity > x.Available ? ValidationText.Pick($"Chỉ còn {x.Available} cuốn khả dụng. Giảm số lượng hoặc xóa dòng.", $"Only {x.Available} copies are available. Reduce the quantity or remove this item.")
                : changed ? ValidationText.Pick("Giá sách đã thay đổi. Kiểm tra giá mới và xác nhận lại khi đặt hàng.", "The price has changed. Check the new price and confirm it at checkout.") : null;
            return new CartLineDto(x.Id, x.BookId, x.Title, x.SKU, x.CoverImage, x.Quantity, x.Price,
                x.Available, !x.Stopped && x.Quantity <= x.Available, warning, changed);
        }).ToList();
        // GET nền cập nhật badge không được tiêu thụ cảnh báo trước khi khách mở giỏ.
        session?.SetString(key, JsonSerializer.Serialize(rows.ToDictionary(x => x.BookId,
            x => prices.GetValueOrDefault(x.BookId, x.Price))));
        return new(lines);
    }

    public Task<CartDto> AddAsync(int userId, CartAddInput input, CancellationToken ct) => Write(userId, async () =>
    {
        var book = await db.Books.AsNoTracking().Include(x => x.Category).Include(x => x.Inventory)
            .SingleOrDefaultAsync(x => x.Id == input.BookId, ct) ?? throw AdminErrors.BookMissing(input.BookId);
        var cart = await db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is null) { cart = new Cart { UserId = userId }; db.Carts.Add(cart); }
        var item = cart.Items.SingleOrDefault(x => x.BookId == input.BookId);
        var inCart = item?.Quantity ?? 0;
        var total = (long)inCart + input.Quantity;
        var available = book.Inventory.Quantity - book.Inventory.ReservedQuantity;
        // Thêm cộng dồn vào dòng có sẵn: báo rõ giỏ đã có bao nhiêu và còn thêm được tối đa bao nhiêu.
        if (inCart > 0 && total > available && !book.IsDeleted && !book.Category.IsDeleted)
        {
            var canAdd = available - inCart;
            throw AdminErrors.Field("CART_STOCK_INSUFFICIENT", "Quantity", canAdd > 0
                ? ValidationText.Pick(
                    $"Giỏ đã có {inCart} cuốn '{book.Title}'; chỉ còn thêm được tối đa {canAdd} cuốn (khả dụng {available}). Giảm số lượng thêm hoặc chỉnh trong giỏ hàng.",
                    $"Your cart already has {inCart} of '{book.Title}'; you can add at most {canAdd} more ({available} available). Reduce the quantity or edit your cart.")
                : canAdd == 0
                    ? ValidationText.Pick(
                        $"Giỏ đã có {inCart} cuốn '{book.Title}', bằng toàn bộ số khả dụng ({available}). Không thể thêm nữa; xem lại trong giỏ hàng.",
                        $"Your cart already has {inCart} of '{book.Title}', all {available} available copies. You cannot add more; review your cart.")
                    // Tồn kho giảm sau khi khách đã thêm vào giỏ: số trong giỏ vượt số khả dụng hiện tại.
                    : ValidationText.Pick(
                        $"Giỏ đang có {inCart} cuốn '{book.Title}', vượt số khả dụng hiện tại ({available}). Vào giỏ hàng giảm xuống tối đa {available} cuốn.",
                        $"Your cart has {inCart} of '{book.Title}', more than the {available} now available. Reduce it to at most {available} in your cart."), 409);
        }
        ValidateQuantity(book, total);
        if (item is null) cart.Items.Add(new CartItem { BookId = input.BookId, Quantity = (int)total });
        else item.Quantity = (int)total;
        cart.UpdatedAt = DateTime.UtcNow;
    }, ct);

    public Task<CartDto> UpdateAsync(int userId, int itemId, int quantity, CancellationToken ct) => Write(userId, async () =>
    {
        var item = await db.CartItems.Include(x => x.Cart).Include(x => x.Book).ThenInclude(x => x.Category)
            .Include(x => x.Book).ThenInclude(x => x.Inventory).SingleOrDefaultAsync(x => x.Id == itemId && x.Cart.UserId == userId, ct)
            ?? throw new AppException(404, "CART_ITEM_NOT_FOUND", ValidationText.Pick(
                "Không tìm thấy dòng sách trong giỏ của bạn. Tải lại giỏ trước khi thao tác.", "This item was not found in your cart. Reload your cart before continuing."));
        if (quantity == 0) db.CartItems.Remove(item);
        else { ValidateQuantity(item.Book, quantity); item.Quantity = quantity; }
        item.Cart.UpdatedAt = DateTime.UtcNow;
    }, ct);

    public Task<CartDto> ClearAsync(int userId, CancellationToken ct) => Write(userId, async () =>
    {
        var cart = await db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is not null) { db.CartItems.RemoveRange(cart.Items); cart.UpdatedAt = DateTime.UtcNow; }
    }, ct);

    private async Task<CartDto> Write(int userId, Func<Task> operation, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await CommerceSupport.LockUserAsync(db, userId, ct);
            await operation();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (CommerceSupport.IsWriteConflict(ex)) { throw CommerceSupport.WriteConflict(); }
        return await GetAsync(userId, ct);
    }

    private static void ValidateQuantity(Book book, long quantity)
    {
        if (quantity <= 0) throw AdminErrors.Field("CART_QUANTITY_INVALID", "Quantity", ValidationText.Pick("Khi thêm sách, số lượng phải là số nguyên dương. Hãy nhập từ 1 trở lên.", "Enter a positive whole quantity when adding a book."));
        if (book.IsDeleted || book.Category.IsDeleted) throw CommerceSupport.Conflict("CART_BOOK_STOPPED",
            $"Không thể thêm/sửa sách '{book.Title}' vì đã ngừng bán. Chọn sách khác hoặc xóa dòng này.",
            $"'{book.Title}' is no longer for sale. Choose another book or remove the item.");
        var available = book.Inventory.Quantity - book.Inventory.ReservedQuantity;
        if (quantity > available) throw AdminErrors.Field("CART_STOCK_INSUFFICIENT", "Quantity", ValidationText.Pick(
            $"Sách '{book.Title}' chỉ còn {available} cuốn khả dụng. Nhập số lượng không vượt mức này.",
            $"Only {available} copies of '{book.Title}' are available. Reduce the quantity."), 409);
    }
}
