using System.Linq.Expressions;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class DatabaseReadiness(IConfiguration configuration)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration.GetConnectionString("BookStore"));
    public void EnsureConfigured()
    {
        if (!IsConfigured) throw new AppException(503, "DATABASE_NOT_CONFIGURED", ValidationText.Pick(
            "Chưa thể truy cập dữ liệu BookStore vì chưa cấu hình SQL Server. Quản trị viên cần đặt ConnectionStrings__BookStore và tạo schema theo README.md.",
            "BookStore cannot access data because SQL Server is not configured. Set ConnectionStrings__BookStore and create the schema as described in README.md."));
    }
}

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct);
    Task<PagedResult<BookCardDto>> SearchAsync(BookQuery query, CancellationToken ct);
    Task<BookDetailDto> DetailAsync(int id, CancellationToken ct);
}

public sealed class CatalogService(BookStoreDbContext db, DatabaseReadiness readiness) : ICatalogService
{
    private static readonly Expression<Func<Book, BookCardDto>> Card = x => new(
        x.Id, x.Title, x.Author, x.SKU, x.Category.Name, x.Price, x.DiscountPrice,
        x.CoverImage, x.Inventory.Quantity - x.Inventory.ReservedQuantity);

    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct)
    {
        readiness.EnsureConfigured();
        return await db.Categories.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CategoryDto(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<PagedResult<BookCardDto>> SearchAsync(BookQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var books = db.Books.AsNoTracking().Where(x => !x.IsDeleted && !x.Category.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            books = books.Where(x => x.Title.Contains(search) || x.Author.Contains(search)
                || x.SKU.Contains(search) || (x.ISBN != null && x.ISBN.Contains(search)));
        }
        if (query.CategoryId.HasValue) books = books.Where(x => x.CategoryId == query.CategoryId);
        if (query.MinPrice.HasValue) books = books.Where(x => (x.DiscountPrice ?? x.Price) >= query.MinPrice);
        if (query.MaxPrice.HasValue) books = books.Where(x => (x.DiscountPrice ?? x.Price) <= query.MaxPrice);
        if (query.InStock) books = books.Where(x => x.Inventory.Quantity > x.Inventory.ReservedQuantity);
        var total = await books.CountAsync(ct);
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)query.PageSize)));
        var ordered = query.Sort switch
        {
            "price-asc" => books.OrderBy(x => x.DiscountPrice ?? x.Price).ThenBy(x => x.Id),
            "price-desc" => books.OrderByDescending(x => x.DiscountPrice ?? x.Price).ThenBy(x => x.Id),
            "title" => books.OrderBy(x => x.Title).ThenBy(x => x.Id),
            _ => books.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
        var items = await ordered.Skip((page - 1) * query.PageSize).Take(query.PageSize).Select(Card).ToListAsync(ct);
        return new(items, page, query.PageSize, total);
    }

    public async Task<BookDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var book = await db.Books.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted && !x.Category.IsDeleted)
            .Select(x => new
            {
                Card = new BookCardDto(x.Id, x.Title, x.Author, x.SKU, x.Category.Name,
                    x.Price, x.DiscountPrice, x.CoverImage, x.Inventory.Quantity - x.Inventory.ReservedQuantity),
                x.Publisher, x.ISBN, x.PublishedYear, x.Description,
                Average = x.Reviews.Where(r => !r.IsHidden).Average(r => (double?)r.Rating),
                Count = x.Reviews.Count(r => !r.IsHidden)
            }).SingleOrDefaultAsync(ct);
        if (book is null) throw new AppException(404, "BOOK_NOT_FOUND", ValidationText.Pick(
            $"Không tìm thấy sách #{id} đang được bán. Hãy chọn sách khác trong danh mục.",
            $"Book #{id} is unavailable or no longer for sale. Choose another book from the catalog."));
        // Chặn tải không giới hạn nội dung đánh giá trong một request.
        var reviews = await db.Reviews.AsNoTracking().Where(x => x.BookId == id && !x.IsHidden)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(20)
            .Select(x => new ReviewDto(x.User.UserName!, x.Rating, x.Comment, x.CreatedAt)).ToListAsync(ct);
        return new(book.Card, book.Publisher, book.ISBN, book.PublishedYear, book.Description,
            book.Average, book.Count, reviews);
    }
}
