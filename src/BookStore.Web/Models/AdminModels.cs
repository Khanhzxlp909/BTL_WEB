using System.ComponentModel.DataAnnotations;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Models;

public sealed class FieldRequiredAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;
    public override string FormatErrorMessage(string name) => ValidationText.Required;
}
public sealed class TextLimitAttribute(int length) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text && text.Length <= length;
    public override string FormatErrorMessage(string name) => ValidationText.Pick($"{FieldLabels.Of(name)} tối đa {length} ký tự. Hãy rút ngắn dữ liệu.", $"{FieldLabels.Of(name)} allows at most {length} characters. Shorten the value.");
}
public sealed class PositiveIntegerAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is int number && number > 0;
    public override string FormatErrorMessage(string name) => name == "CategoryId"
        ? ValidationText.Pick("Hãy chọn thể loại sách từ danh sách.", "Select a book category from the list.")
        : ValidationText.Pick($"{FieldLabels.Of(name)} phải là số nguyên dương. Hãy nhập từ 1 trở lên.", $"{FieldLabels.Of(name)} must be a positive integer. Enter at least 1.");
}

// Tên trường hiển thị cho người dùng trong thông báo lỗi, thay cho tên thuộc tính kỹ thuật.
public static class FieldLabels
{
    private static readonly Dictionary<string, (string Vi, string En)> Labels = new()
    {
        ["Title"] = ("Tên sách", "Title"), ["Author"] = ("Tác giả", "Author"), ["Publisher"] = ("Nhà xuất bản", "Publisher"),
        ["ISBN"] = ("ISBN", "ISBN"), ["SKU"] = ("Mã SKU", "SKU"), ["CategoryId"] = ("Thể loại", "Category"),
        ["Name"] = ("Tên thể loại", "Category name"), ["Description"] = ("Mô tả", "Description"),
        ["Quantity"] = ("Số lượng", "Quantity"), ["BookId"] = ("Mã sách", "Book ID"), ["Reference"] = ("Mã phiếu nhập", "Reference"),
        ["Reason"] = ("Lý do", "Reason"), ["RowVersion"] = ("Phiên bản tồn kho", "Inventory version"),
        ["RecipientName"] = ("Tên người nhận", "Recipient name"), ["Phone"] = ("Số điện thoại", "Phone"),
        ["ShippingAddress"] = ("Địa chỉ giao hàng", "Shipping address"), ["Note"] = ("Ghi chú", "Note"),
        ["Comment"] = ("Nội dung đánh giá", "Review comment"), ["Search"] = ("Từ khóa", "Keyword"),
        ["Customer"] = ("Khách hàng", "Customer"), ["Page"] = ("Trang", "Page"), ["PageSize"] = ("Số mục mỗi trang", "Page size")
    };

    public static string Of(string name) => Labels.TryGetValue(name, out var label) ? ValidationText.Pick(label.Vi, label.En) : name;
}

public class BookInput : IValidatableObject
{
    [FieldRequired, TextLimit(255)] public string Title { get; set; } = "";
    [FieldRequired, TextLimit(150)] public string Author { get; set; } = "";
    [TextLimit(150)] public string? Publisher { get; set; }
    [TextLimit(30)] public string? ISBN { get; set; }
    [FieldRequired, TextLimit(64)] public string SKU { get; set; } = "";
    [PositiveInteger] public int CategoryId { get; set; }
    public int? PublishedYear { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price <= 0 || Price > 999999999999999999m || Price % 1 != 0)
            yield return new(ValidationText.Pick("Giá gốc phải là số nguyên đồng từ 1 đến 999999999999999999. Hãy nhập lại giá.", "Enter a whole VND price from 1 to 999999999999999999."), [nameof(Price)]);
        if (DiscountPrice.HasValue && (DiscountPrice <= 0 || DiscountPrice > Price || DiscountPrice % 1 != 0))
            yield return new(ValidationText.Pick("Giá khuyến mãi phải là số nguyên đồng dương, không vượt giá gốc; hoặc để trống.", "The discount price must be a positive whole VND amount no greater than the regular price, or left empty."), [nameof(DiscountPrice)]);
        if (PublishedYear.HasValue && (PublishedYear < 1 || PublishedYear > DateTime.UtcNow.AddHours(7).Year))
            yield return new(ValidationText.Pick("Năm xuất bản phải từ 1 đến năm hiện tại. Hãy sửa hoặc để trống.", "The publication year must be between 1 and the current year, or left empty."), [nameof(PublishedYear)]);
    }
}

public sealed class BookFormViewModel : BookInput
{
    public int? Id { get; set; }
    public IFormFile? CoverFile { get; set; }
    public string? ExistingCoverImage { get; set; }
    public IReadOnlyList<CategoryDto> Categories { get; set; } = [];
}
public sealed record AdminBookDto(int Id, string Title, string Author, string SKU, string CategoryName,
    decimal Price, decimal? DiscountPrice, string? CoverImage, bool IsDeleted, int Quantity, int ReservedQuantity);
public sealed record AdminBookListViewModel(BookQuery Query, string Status, PagedResult<AdminBookDto> Books, IReadOnlyList<CategoryDto> Categories);
public sealed record BookSavedDto(int Id, string Title, string SKU, bool IsDeleted);

public sealed class CategoryInput
{
    [FieldRequired, TextLimit(100)] public string Name { get; set; } = "";
    [TextLimit(1000)] public string? Description { get; set; }
}
public sealed record AdminCategoryDto(int Id, string Name, string? Description, bool IsDeleted, int BookCount);
public sealed record CategoryListViewModel(AdminPageQuery Query, PagedResult<AdminCategoryDto> Categories);
public sealed record CategoryFormViewModel(int? Id, CategoryInput Input);

public class AdminPageQuery : IValidatableObject
{
    [TextLimit(255)] public string? Search { get; set; }
    [PositiveInteger] public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PageSize is < 1 or > 100) yield return new(ValidationText.PageSize, [nameof(PageSize)]);
    }
}
public sealed class InventoryQuery : AdminPageQuery
{
    public bool LowStockOnly { get; set; }
}
public sealed record InventoryDto(int BookId, string Title, string SKU, bool IsDeleted, int Quantity,
    int ReservedQuantity, string RowVersion)
{
    public int AvailableQuantity => Quantity - ReservedQuantity;
}
public sealed record InventoryListViewModel(InventoryQuery Query, PagedResult<InventoryDto> Inventories, int LowStockThreshold);
public sealed class ImportInput
{
    [PositiveInteger] public int Quantity { get; set; }
    [FieldRequired, TextLimit(64)] public string Reference { get; set; } = "";
    [FieldRequired, TextLimit(500)] public string Reason { get; set; } = "";
    [FieldRequired, TextLimit(32)] public string RowVersion { get; set; } = "";
}
public sealed record ImportViewModel(InventoryDto Inventory, ImportInput Input);
public sealed class ThresholdInput : IValidatableObject
{
    public int Value { get; set; } = 5;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Value < 0) yield return new(ValidationText.Pick("Ngưỡng tồn thấp phải là số nguyên không âm. Hãy nhập từ 0 trở lên.", "The low-stock threshold must be a non-negative integer."), [nameof(Value)]);
    }
}
public sealed record InventoryHistoryDto(DateTime CreatedAt, InventoryTransactionType Type, int QuantityDelta,
    int ReservedDelta, int QuantityBefore, int QuantityAfter, int ReservedBefore, int ReservedAfter,
    string ActorUserName, string? Reference, string? OrderCode, string? Reason);
public sealed record InventoryHistoryViewModel(InventoryDto Inventory, AdminPageQuery Query, PagedResult<InventoryHistoryDto> History);

public interface IAdminCatalogService
{
    Task<AdminBookListViewModel> BooksAsync(BookQuery query, string status, CancellationToken ct);
    Task<BookFormViewModel> BookFormAsync(int? id, CancellationToken ct);
    Task<BookSavedDto> SaveBookAsync(int? id, BookInput input, IFormFile? cover, CancellationToken ct);
    Task<BookSavedDto> SetBookDeletedAsync(int id, bool deleted, CancellationToken ct);
    Task<PagedResult<AdminCategoryDto>> CategoriesAsync(AdminPageQuery query, CancellationToken ct);
    Task<CategoryInput> CategoryAsync(int id, CancellationToken ct);
    Task SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct);
    Task DeleteCategoryAsync(int id, CancellationToken ct);
}
public interface IInventoryService
{
    Task<InventoryListViewModel> ListAsync(InventoryQuery query, CancellationToken ct);
    Task<InventoryDto> GetAsync(int bookId, CancellationToken ct);
    Task ImportAsync(int bookId, ImportInput input, int actorId, CancellationToken ct);
    Task<int> ThresholdAsync(CancellationToken ct);
    Task SetThresholdAsync(ThresholdInput input, CancellationToken ct);
    Task<InventoryHistoryViewModel> HistoryAsync(int bookId, AdminPageQuery query, CancellationToken ct);
}
