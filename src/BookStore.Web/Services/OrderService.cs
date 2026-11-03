using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class OrderService(BookStoreDbContext db, DatabaseReadiness readiness, ICartService carts,
    CheckoutQuotes quotes) : IOrderService
{
    public async Task<CheckoutViewModel> CheckoutAsync(int userId, CancellationToken ct)
    {
        var cart = await carts.GetAsync(userId, ct);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        return new(cart, new() { RecipientName = user.FullName ?? "", Phone = user.PhoneNumber ?? "", QuoteToken = quotes.Create(userId, cart) });
    }

    public async Task<OrderDetailDto> PlaceAsync(int userId, CheckoutInput input, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var quote = quotes.Read(userId, input.QuoteToken);
        int id;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await CommerceSupport.LockUserAsync(db, userId, ct);
            var previous = await db.Orders.Where(x => x.OrderCode == quote.OrderCode && x.UserId == userId).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
            if (previous.HasValue) { await transaction.CommitAsync(ct); return await DetailAsync(userId, false, previous.Value, ct); }
            if (quote.ExpiresAt < DateTime.UtcNow) throw CommerceSupport.Conflict("CHECKOUT_QUOTE_EXPIRED",
                "Báo giá đặt hàng đã hết hạn. Tải lại trang đặt hàng, kiểm tra giá và xác nhận lại.", "The quotation expired. Reload checkout, check prices and confirm again.");
            var cart = await db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (cart is null || cart.Items.Count == 0) throw CommerceSupport.Conflict("CHECKOUT_CART_EMPTY",
                "Không thể đặt hàng vì giỏ trống. Thêm sách đang bán vào giỏ trước.", "Your cart is empty. Add an available book before placing an order.");
            // Cùng thứ tự BookId ở mọi thao tác kho của đơn để giảm deadlock.
            var stocks = new Dictionary<int, Inventory>();
            foreach (var line in cart.Items.OrderBy(x => x.BookId)) stocks[line.BookId] = await CommerceSupport.LockInventoryAsync(db, line.BookId, ct);
            var ids = stocks.Keys.ToArray();
            var booksQuery = db.Database.IsSqlServer()
                ? db.Books.FromSqlRaw("SELECT * FROM [Books] WITH (HOLDLOCK)") : db.Books;
            var books = await booksQuery.AsNoTracking().Where(x => ids.Contains(x.Id)).Include(x => x.Category).ToDictionaryAsync(x => x.Id, ct);
            var lines = cart.Items.Select(x =>
            {
                var book = books[x.BookId];
                var stock = stocks[x.BookId];
                if (book.IsDeleted || book.Category.IsDeleted || x.Quantity > stock.Quantity - stock.ReservedQuantity)
                    throw CommerceSupport.Conflict("CHECKOUT_STOCK_UNAVAILABLE",
                        $"Không thể đặt sách '{book.Title}': đã ngừng bán hoặc chỉ còn {stock.Quantity - stock.ReservedQuantity} cuốn khả dụng. Quay lại giỏ để điều chỉnh.",
                        $"'{book.Title}' is stopped or only {stock.Quantity - stock.ReservedQuantity} copies are available. Adjust your cart.");
                return new CartLineDto(x.Id, x.BookId, book.Title, book.SKU, null, x.Quantity,
                    book.DiscountPrice ?? book.Price, stock.Quantity - stock.ReservedQuantity, true, null);
            }).ToList();
            if (CommerceSupport.Fingerprint(lines) != quote.Fingerprint) throw CommerceSupport.Conflict("CHECKOUT_QUOTE_CHANGED",
                "Giỏ hoặc giá sách đã thay đổi so với báo giá đã xem. Chưa tạo đơn. Kiểm tra báo giá mới và xác nhận lại.",
                "Your cart or book prices changed. No order was created. Review the new quotation and confirm again.");
            var total = lines.Sum(x => x.LineTotal);
            if (total > CommerceSupport.MoneyLimit) throw CommerceSupport.Conflict("ORDER_AMOUNT_LIMIT",
                "Tổng đơn vượt giới hạn lưu tiền decimal(18,0). Giảm số lượng hoặc chia thành các đơn nhỏ hơn.",
                "The order total exceeds the decimal(18,0) storage limit. Reduce quantities or split the purchase.");
            var order = new Order
            {
                UserId = userId, OrderCode = quote.OrderCode, RecipientName = input.RecipientName.Trim(),
                Phone = input.Phone.Trim(), ShippingAddress = input.ShippingAddress.Trim(), Note = input.Note?.Trim(),
                Subtotal = total, TotalAmount = total,
                Items = lines.Select(x => new OrderItem { BookId = x.BookId, BookTitle = x.Title, SKU = x.SKU,
                    Quantity = x.Quantity, UnitPrice = x.UnitPrice }).ToList(),
                Events = [new OrderEvent { ActorUserId = userId, ToStatus = OrderStatus.PENDING }]
            };
            db.Orders.Add(order);
            foreach (var line in lines) db.InventoryTransactions.Add(CommerceSupport.Movement(stocks[line.BookId], order,
                userId, InventoryTransactionType.ORDER_PLACED, 0, line.Quantity));
            db.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            id = order.Id;
        }
        catch (Exception ex) when (CommerceSupport.IsWriteConflict(ex)) { throw CommerceSupport.WriteConflict(); }
        return await DetailAsync(userId, false, id, ct);
    }

    public async Task<OrderListViewModel> ListAsync(int userId, bool admin, OrderQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var rows = db.Orders.AsNoTracking().Where(x => admin || x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.OrderCode.Contains(query.Search.Trim()));
        if (admin && !string.IsNullOrWhiteSpace(query.Customer))
        {
            var customer = query.Customer.Trim();
            rows = rows.Where(x => x.User.UserName!.Contains(customer) || x.RecipientName.Contains(customer));
        }
        if (query.Status.HasValue) rows = rows.Where(x => x.Status == query.Status);
        if (query.FromDate.HasValue) { var from = CommerceSupport.StartUtc(query.FromDate.Value); rows = rows.Where(x => x.CreatedAt >= from); }
        if (query.ToDate.HasValue) { var until = CommerceSupport.StartUtc(query.ToDate.Value.AddDays(1)); rows = rows.Where(x => x.CreatedAt < until); }
        var page = await AdminPaging.ReadAsync(rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new OrderSummaryDto(x.Id, x.OrderCode, x.User.UserName!, x.CreatedAt, x.TotalAmount, x.Status, x.PaymentStatus)), query.Page, query.PageSize, ct);
        return new(query, page, admin);
    }

    // Mã đơn trong URL chỉ phân giải trong phạm vi chủ đơn; đơn người khác trả 404 như không tồn tại.
    public async Task<int> ResolveCodeAsync(int userId, string orderCode, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var code = orderCode.Trim();
        return await db.Orders.AsNoTracking().Where(x => x.OrderCode == code && x.UserId == userId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new AppException(404, "ORDER_NOT_FOUND", ValidationText.Pick(
                $"Không tìm thấy đơn hàng {code} trong lịch sử của bạn. Kiểm tra lại mã đơn hoặc mở danh sách đơn của mình.",
                $"Order {code} was not found in your order history. Check the order code or open your order list."));
    }

    public async Task<OrderDetailDto> DetailAsync(int userId, bool admin, int id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var order = await db.Orders.AsNoTracking().Where(x => x.Id == id && (admin || x.UserId == userId))
            .Include(x => x.User).Include(x => x.Items).Include(x => x.Events).ThenInclude(x => x.ActorUser)
            .AsSplitQuery().SingleOrDefaultAsync(ct) ?? throw CommerceSupport.OrderMissing(id);
        return new(order.Id, order.OrderCode, order.User.UserName!, order.Status, order.PaymentStatus, order.PaymentMethod,
            order.RecipientName, order.Phone, order.ShippingAddress, order.Note, order.Subtotal, order.ShippingFee,
            order.TotalAmount, order.CreatedAt, order.DeliveredAt, order.CancelledAt,
            order.Items.OrderBy(x => x.Id).Select(x => new OrderLineDto(x.BookId, x.BookTitle, x.SKU, x.Quantity, x.UnitPrice, x.LineTotal)).ToList(),
            order.Events.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new OrderEventDto(x.FromStatus, x.ToStatus, x.ActorUser.UserName!, x.Note, x.CreatedAt)).ToList());
    }

    public Task CancelAsync(int userId, bool admin, int id, CancelOrderInput input, CancellationToken ct)
        => ChangeAsync(userId, admin, id, OrderStatus.CANCELLED, false, input.Reason, ct);
    public Task TransitionAsync(int actorId, int id, OrderStatusInput input, CancellationToken ct)
        => ChangeAsync(actorId, true, id, input.Status, input.ConfirmCodCollected, null, ct);

    private async Task ChangeAsync(int actorId, bool admin, int id, OrderStatus target, bool cod, string? reason, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await CommerceSupport.LockUserAsync(db, actorId, ct);
            var rows = db.Database.IsSqlServer() ? db.Orders.FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}") : db.Orders.Where(x => x.Id == id);
            var order = await rows.Include(x => x.Items).SingleOrDefaultAsync(x => admin || x.UserId == actorId, ct) ?? throw CommerceSupport.OrderMissing(id);
            if (order.Status == target) { await transaction.CommitAsync(ct); return; }
            if (target == OrderStatus.CANCELLED)
            {
                if (order.Status is not (OrderStatus.PENDING or OrderStatus.CONFIRMED)) throw InvalidTransition(order, target);
                var actorIsAdmin = admin || await db.UserRoles.AnyAsync(ur => ur.UserId == actorId
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Admin), ct);
                if (actorIsAdmin && string.IsNullOrWhiteSpace(reason)) throw AdminErrors.Field("ADMIN_CANCEL_REASON_REQUIRED", "Reason",
                    ValidationText.Pick("Admin phải nhập lý do hủy đơn. Nhập tối đa 500 ký tự rồi xác nhận lại.", "An administrator must provide a cancellation reason of at most 500 characters."));
            }
            else if (!admin || (order.Status, target) is not ((OrderStatus.PENDING, OrderStatus.CONFIRMED)
                or (OrderStatus.CONFIRMED, OrderStatus.SHIPPING) or (OrderStatus.SHIPPING, OrderStatus.DELIVERED))) throw InvalidTransition(order, target);
            if (target == OrderStatus.DELIVERED && !cod) throw AdminErrors.Field("COD_CONFIRMATION_REQUIRED", "ConfirmCodCollected",
                ValidationText.Pick("Chưa thể ghi nhận đã giao vì chưa xác nhận đã giao và thu tiền COD. Chọn xác nhận rồi gửi lại.", "Confirm delivery and collection of COD payment before marking the order delivered."));
            if (target is OrderStatus.CANCELLED or OrderStatus.DELIVERED)
                foreach (var item in order.Items.OrderBy(x => x.BookId))
                {
                    var stock = await CommerceSupport.LockInventoryAsync(db, item.BookId, ct);
                    if (stock.ReservedQuantity < item.Quantity || stock.Quantity < item.Quantity) throw CommerceSupport.Conflict("ORDER_INVENTORY_INCONSISTENT",
                        $"Không thể xử lý đơn {order.OrderCode}: tồn/giữ chỗ của sách '{item.BookTitle}' không đủ đối chiếu. Liên hệ quản trị viên kiểm tra lịch sử kho.",
                        $"Order {order.OrderCode} cannot be processed: stock or reservation for '{item.BookTitle}' is inconsistent. Ask an administrator to check inventory history.");
                    db.InventoryTransactions.Add(CommerceSupport.Movement(stock, order, actorId,
                        target == OrderStatus.DELIVERED ? InventoryTransactionType.ORDER_DELIVERED : InventoryTransactionType.ORDER_CANCELLED,
                        target == OrderStatus.DELIVERED ? -item.Quantity : 0, -item.Quantity, reason?.Trim()));
                }
            db.OrderEvents.Add(new OrderEvent { OrderId = id, ActorUserId = actorId, FromStatus = order.Status, ToStatus = target, Note = reason?.Trim() });
            order.Status = target;
            if (target == OrderStatus.CANCELLED) order.CancelledAt = DateTime.UtcNow;
            if (target == OrderStatus.DELIVERED) { order.DeliveredAt = DateTime.UtcNow; order.PaymentStatus = PaymentStatus.PAID; }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (CommerceSupport.IsWriteConflict(ex)) { throw CommerceSupport.WriteConflict(); }
    }

    private static AppException InvalidTransition(Order order, OrderStatus target) => CommerceSupport.Conflict("ORDER_STATUS_TRANSITION_INVALID",
        $"Không thể chuyển đơn {order.OrderCode} từ {order.Status} sang {target}. Tải lại chi tiết và chọn bước kế tiếp hợp lệ; chỉ hủy khi PENDING/CONFIRMED.",
        $"Order {order.OrderCode} cannot move from {order.Status} to {target}. Reload the order and select a valid next step; cancellation is allowed only for PENDING/CONFIRMED.");
}
