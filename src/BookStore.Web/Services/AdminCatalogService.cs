using System.Data;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class AdminCatalogService(BookStoreDbContext db, DatabaseReadiness readiness,
    ICoverImageStorage images) : IAdminCatalogService
{
    public async Task<AdminBookListViewModel> BooksAsync(BookQuery query, string status, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        if (status is not ("all" or "active" or "deleted")) throw AdminErrors.Field("BOOK_STATUS_INVALID", "Status",
            ValidationText.Pick("Bộ lọc sách không hợp lệ. Chọn all, active hoặc deleted.", "Choose a valid book status: all, active or deleted."));
        var books = db.Books.AsNoTracking();
        if (status != "all") books = books.Where(x => x.IsDeleted == (status == "deleted"));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            books = books.Where(x => x.Title.Contains(search) || x.Author.Contains(search) || x.SKU.Contains(search) || x.ISBN != null && x.ISBN.Contains(search));
        }
        if (query.CategoryId.HasValue) books = books.Where(x => x.CategoryId == query.CategoryId);
        if (query.MinPrice.HasValue) books = books.Where(x => (x.DiscountPrice ?? x.Price) >= query.MinPrice);
        if (query.MaxPrice.HasValue) books = books.Where(x => (x.DiscountPrice ?? x.Price) <= query.MaxPrice);
        if (query.InStock) books = books.Where(x => x.Inventory.Quantity > x.Inventory.ReservedQuantity);
        var ordered = query.Sort switch
        {
            "price-asc" => books.OrderBy(x => x.DiscountPrice ?? x.Price).ThenBy(x => x.Id),
            "price-desc" => books.OrderByDescending(x => x.DiscountPrice ?? x.Price).ThenBy(x => x.Id),
            "title" => books.OrderBy(x => x.Title).ThenBy(x => x.Id),
            _ => books.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
        var rows = ordered.Select(x => new AdminBookDto(x.Id, x.Title, x.Author, x.SKU, x.Category.Name,
            x.Price, x.DiscountPrice, x.CoverImage, x.IsDeleted, x.Inventory.Quantity, x.Inventory.ReservedQuantity));
        return new(query, status, await AdminPaging.ReadAsync(rows, query.Page, query.PageSize, ct), await CategoryOptions(ct));
    }

    public async Task<BookFormViewModel> BookFormAsync(int? id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var form = new BookFormViewModel { Categories = await CategoryOptions(ct) };
        if (id is null) return form;
        var book = await db.Books.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AdminErrors.BookMissing(id.Value);
        form.Id = book.Id; form.Title = book.Title; form.Author = book.Author; form.SKU = book.SKU;
        form.Publisher = book.Publisher; form.ISBN = book.ISBN; form.CategoryId = book.CategoryId;
        form.PublishedYear = book.PublishedYear; form.Price = book.Price; form.DiscountPrice = book.DiscountPrice;
        form.Description = book.Description; form.ExistingCoverImage = book.CoverImage;
        return form;
    }

    public async Task<BookSavedDto> SaveBookAsync(int? id, BookInput input, IFormFile? cover, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId && !x.IsDeleted, ct))
            throw AdminErrors.Field("BOOK_CATEGORY_UNAVAILABLE", "CategoryId", ValidationText.Pick(
                "Thể loại đã chọn không tồn tại hoặc đã ngừng sử dụng. Chọn thể loại đang hoạt động.", "The selected category is missing or inactive. Choose an active category."));
        var sku = input.SKU.Trim().ToUpperInvariant();
        if (await db.Books.AnyAsync(x => x.SKU == sku && x.Id != id, ct)) throw DuplicateSku();
        var book = id.HasValue ? await db.Books.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AdminErrors.BookMissing(id.Value)
            : new Book { Inventory = new Inventory() };
        string? stored = null;
        try
        {
            if (cover is not null) stored = await images.SaveAsync(cover, ct);
            book.Title = input.Title.Trim(); book.Author = input.Author.Trim(); book.SKU = sku;
            book.CategoryId = input.CategoryId; book.Publisher = input.Publisher?.Trim(); book.ISBN = input.ISBN?.Trim();
            book.PublishedYear = input.PublishedYear; book.Price = input.Price; book.DiscountPrice = input.DiscountPrice;
            book.Description = input.Description; book.UpdatedAt = DateTime.UtcNow;
            if (stored is not null) book.CoverImage = stored;
            if (!id.HasValue) db.Books.Add(book);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(book.Id, book.Title, book.SKU, book.IsDeleted);
        }
        catch (Exception failure)
        {
            if (stored is not null)
            {
                try { await images.DeleteAsync(stored, CancellationToken.None); }
                catch (Exception cleanup) { throw new AggregateException("Lưu sách thất bại và không thể dọn ảnh bìa mới. Kiểm tra kho ảnh theo mã tra cứu.", failure, cleanup); }
            }
            if (failure is DbUpdateException update && AdminErrors.IsUniqueViolation(update)) throw DuplicateSku();
            throw;
        }
    }

    public async Task<BookSavedDto> SetBookDeletedAsync(int id, bool deleted, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var book = await db.Books.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AdminErrors.BookMissing(id);
        if (book.IsDeleted == deleted) throw new AppException(409, deleted ? "BOOK_ALREADY_STOPPED" : "BOOK_NOT_STOPPED", ValidationText.Pick(
            deleted ? $"Sách #{id} ({book.SKU}) đã ở trạng thái ngừng bán; không có thay đổi. Tải lại danh sách để xem trạng thái hiện tại."
                    : $"Sách #{id} ({book.SKU}) đang được bán, không cần khôi phục. Tải lại danh sách để xem trạng thái hiện tại.",
            deleted ? $"Book #{id} ({book.SKU}) is already stopped; nothing changed. Reload the list to see its current status."
                    : $"Book #{id} ({book.SKU}) is already on sale and needs no restoring. Reload the list to see its current status."));
        {
            if (!deleted && !await db.Categories.AnyAsync(x => x.Id == book.CategoryId && !x.IsDeleted, ct))
                throw AdminErrors.Field("BOOK_CATEGORY_UNAVAILABLE", "CategoryId", ValidationText.Pick("Không thể khôi phục sách vì thể loại không hoạt động. Sửa sách sang thể loại đang hoạt động trước.", "The category is inactive. Assign an active category before restoring the book."), 409);
            book.IsDeleted = deleted; book.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(book.Id, book.Title, book.SKU, book.IsDeleted);
    }

    public async Task<PagedResult<AdminCategoryDto>> CategoriesAsync(AdminPageQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var rows = db.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Name.Contains(query.Search.Trim()));
        return await AdminPaging.ReadAsync(rows.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new AdminCategoryDto(x.Id, x.Name, x.Description, x.IsDeleted, x.Books.Count)), query.Page, query.PageSize, ct);
    }

    public async Task<CategoryInput> CategoryAsync(int id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        return await db.Categories.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new CategoryInput { Name = x.Name, Description = x.Description }).SingleOrDefaultAsync(ct) ?? throw AdminErrors.CategoryMissing(id);
    }

    public async Task SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var name = input.Name.Trim();
        if (await db.Categories.AnyAsync(x => x.Name == name && x.Id != id, ct)) throw DuplicateCategory();
        var category = id.HasValue ? await db.Categories.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw AdminErrors.CategoryMissing(id.Value) : new Category();
        category.Name = name; category.Description = input.Description?.Trim();
        if (!id.HasValue) db.Categories.Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (AdminErrors.IsUniqueViolation(ex)) { throw DuplicateCategory(); }
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AdminErrors.CategoryMissing(id);
        if (await db.Books.AnyAsync(x => x.CategoryId == id, ct)) throw new AppException(409, "CATEGORY_HAS_BOOKS", ValidationText.Pick(
            $"Không thể xóa thể loại #{id} vì còn sách tham chiếu, kể cả sách ngừng bán. Chuyển toàn bộ sách sang thể loại khác trước.",
            $"Category #{id} still has books, including stopped books. Move all books to another category before deleting it."));
        category.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<IReadOnlyList<CategoryDto>> CategoryOptions(CancellationToken ct) => await db.Categories.AsNoTracking()
        .Where(x => !x.IsDeleted).OrderBy(x => x.Name).Select(x => new CategoryDto(x.Id, x.Name)).ToListAsync(ct);
    private static AppException DuplicateSku() => AdminErrors.Field("BOOK_SKU_DUPLICATE", "SKU", ValidationText.Pick("Mã SKU đã được sử dụng, kể cả bởi sách ngừng bán. Hãy chọn mã sách khác.", "This SKU is already used, including by a stopped book. Choose another SKU."), 409);
    private static AppException DuplicateCategory() => AdminErrors.Field("CATEGORY_NAME_DUPLICATE", "Name", ValidationText.Pick("Tên thể loại đã được sử dụng, kể cả thể loại đã xóa. Hãy chọn tên khác.", "This category name is already used, including by a deleted category. Choose another name."), 409);
}

public static class AdminPaging
{
    public static async Task<PagedResult<T>> ReadAsync<T>(IQueryable<T> rows, int requestedPage, int pageSize, CancellationToken ct)
    {
        var total = await rows.CountAsync(ct);
        var page = Math.Clamp(requestedPage, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        return new(await rows.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), page, pageSize, total);
    }
}
