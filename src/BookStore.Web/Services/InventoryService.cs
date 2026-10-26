using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class InventoryService(BookStoreDbContext db, DatabaseReadiness readiness) : IInventoryService
{
    private IQueryable<Inventory> Rows => db.Inventories.AsNoTracking();
    private static InventoryDto Dto(Inventory inventory) => new(inventory.BookId, inventory.Book.Title,
        inventory.Book.SKU, inventory.Book.IsDeleted, inventory.Quantity, inventory.ReservedQuantity,
        Convert.ToBase64String(inventory.RowVersion));

    public async Task<InventoryListViewModel> ListAsync(InventoryQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var threshold = await ThresholdAsync(ct);
        var rows = Rows;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Book.Title.Contains(search) || x.Book.SKU.Contains(search));
        }
        if (query.LowStockOnly) rows = rows.Where(x => x.Quantity - x.ReservedQuantity <= threshold);
        var page = await AdminPaging.ReadAsync(rows.OrderBy(x => x.Book.Title).ThenBy(x => x.BookId)
            .Select(x => new { x.BookId, x.Book.Title, x.Book.SKU, x.Book.IsDeleted, x.Quantity, x.ReservedQuantity, x.RowVersion }),
            query.Page, query.PageSize, ct);
        return new(query, new(page.Items.Select(x => new InventoryDto(x.BookId, x.Title, x.SKU, x.IsDeleted,
            x.Quantity, x.ReservedQuantity, Convert.ToBase64String(x.RowVersion))).ToList(), page.Page, page.PageSize, page.TotalItems), threshold);
    }

    public async Task<InventoryDto> GetAsync(int bookId, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var inventory = await Rows.Include(x => x.Book).SingleOrDefaultAsync(x => x.BookId == bookId, ct)
            ?? throw AdminErrors.BookMissing(bookId);
        return Dto(inventory);
    }

    public async Task ImportAsync(int bookId, ImportInput input, int actorId, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        byte[] version;
        try { version = Convert.FromBase64String(input.RowVersion); }
        catch (FormatException)
        {
            throw AdminErrors.Field("INVENTORY_VERSION_INVALID", "RowVersion", ValidationText.Pick(
                "Phiên bản tồn kho không hợp lệ. Tải lại biểu mẫu nhập kho để nhận số liệu mới.",
                "The inventory version is invalid. Reload the import form to obtain current data."));
        }
        if (version.Length != 8) throw AdminErrors.Field("INVENTORY_VERSION_INVALID", "RowVersion", ValidationText.Pick(
            "Phiên bản tồn kho phải có 8 byte. Tải lại biểu mẫu nhập kho rồi thực hiện lại.",
            "The inventory version must contain 8 bytes. Reload the import form and try again."));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var inventory = await db.Inventories.SingleOrDefaultAsync(x => x.BookId == bookId, ct) ?? throw AdminErrors.BookMissing(bookId);
        if (!inventory.RowVersion.AsSpan().SequenceEqual(version)) throw Stale();
        if (input.Quantity > int.MaxValue - inventory.Quantity) throw AdminErrors.Field("INVENTORY_QUANTITY_OVERFLOW", "Quantity",
            ValidationText.Pick("Số nhập làm tồn kho vượt giới hạn số nguyên của hệ thống. Hãy giảm số lượng nhập.", "The import would exceed the inventory integer limit. Reduce the imported quantity."));
        db.Entry(inventory).Property(x => x.RowVersion).OriginalValue = version;
        var before = inventory.Quantity;
        inventory.Quantity += input.Quantity;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryId = inventory.Id, ActorUserId = actorId, Type = InventoryTransactionType.IMPORT,
            QuantityDelta = input.Quantity, ReservedDelta = 0, QuantityBefore = before, QuantityAfter = inventory.Quantity,
            ReservedBefore = inventory.ReservedQuantity, ReservedAfter = inventory.ReservedQuantity,
            Reference = input.Reference.Trim(), Reason = input.Reason.Trim()
        });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw Stale(); }
    }

    public async Task<int> ThresholdAsync(CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var setting = await db.SystemSettings.AsNoTracking().Where(x => x.SettingKey == "LowStockThreshold").Select(x => x.SettingValue).SingleOrDefaultAsync(ct);
        if (!int.TryParse(setting, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new AppException(503, "LOW_STOCK_THRESHOLD_NOT_INITIALIZED", ValidationText.Pick(
                "Chưa thể đọc cảnh báo kho vì ngưỡng chung bị thiếu hoặc không phải số nguyên không âm. Quản trị viên cần khởi tạo hệ thống hoặc sửa ngưỡng tại /admin/kho/nguong.",
                "The common low-stock threshold is missing or invalid. Initialize the system or save a valid value at /admin/kho/nguong."));
        return value;
    }

    public async Task SetThresholdAsync(ThresholdInput input, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.SettingKey == "LowStockThreshold", ct);
        if (setting is null)
        {
            setting = new SystemSetting { SettingKey = "LowStockThreshold" };
            db.SystemSettings.Add(setting);
        }
        setting.SettingValue = input.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (AdminErrors.IsUniqueViolation(ex))
        {
            throw new AppException(409, "LOW_STOCK_THRESHOLD_CONFLICT", ValidationText.Pick(
                "Ngưỡng kho vừa được khởi tạo bởi quản trị viên khác. Tải lại trang rồi lưu lại giá trị mong muốn.",
                "Another administrator initialized the threshold. Reload the page and save your intended value."));
        }
    }

    public async Task<InventoryHistoryViewModel> HistoryAsync(int bookId, AdminPageQuery query, CancellationToken ct)
    {
        var inventory = await GetAsync(bookId, ct);
        var rows = db.InventoryTransactions.AsNoTracking().Where(x => x.Inventory.BookId == bookId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new InventoryHistoryDto(x.CreatedAt, x.Type, x.QuantityDelta, x.ReservedDelta,
                x.QuantityBefore, x.QuantityAfter, x.ReservedBefore, x.ReservedAfter, x.ActorUser.UserName!,
                x.Reference, x.Order == null ? null : x.Order.OrderCode, x.Reason));
        return new(inventory, query, await AdminPaging.ReadAsync(rows, query.Page, query.PageSize, ct));
    }

    private static AppException Stale() => new(409, "INVENTORY_CONCURRENCY_CONFLICT", ValidationText.Pick(
        "Không thể nhập kho vì số liệu đã thay đổi từ lúc mở biểu mẫu. Chưa ghi nhận lần nhập này. Tải lại biểu mẫu, kiểm tra tồn mới rồi xác nhận lại.",
        "Inventory has changed since you opened the form. This import was not saved. Reload the form, check the current stock and confirm again."));
}
