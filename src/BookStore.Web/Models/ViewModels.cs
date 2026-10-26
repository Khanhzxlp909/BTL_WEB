using System.ComponentModel.DataAnnotations;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Models;

public sealed record CategoryDto(int Id, string Name);
public sealed record BookCardDto(int Id, string Title, string Author, string SKU, string CategoryName,
    decimal Price, decimal? DiscountPrice, string? CoverImage, int AvailableQuantity)
{
    public decimal SellingPrice => DiscountPrice ?? Price;
}
public sealed record ReviewDto(string UserName, int Rating, string Comment, DateTime CreatedAt);
public sealed record BookDetailDto(BookCardDto Book, string? Publisher, string? ISBN, int? PublishedYear,
    string? Description, double? AverageRating, int ReviewCount, IReadOnlyList<ReviewDto> Reviews);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed class BookQuery : IValidatableObject
{
    [StringLength(255, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    public string? Search { get; set; }
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Positive))]
    public int? CategoryId { get; set; }
    [Range(typeof(decimal), "0", "999999999999999999", ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Price))]
    public decimal? MinPrice { get; set; }
    [Range(typeof(decimal), "0", "999999999999999999", ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Price))]
    public decimal? MaxPrice { get; set; }
    public bool InStock { get; set; }
    public string Sort { get; set; } = "newest";
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Positive))]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.PageSize))]
    public int PageSize { get; set; } = 12;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice > MaxPrice)
            yield return new ValidationResult(ValidationText.PriceRange, [nameof(MaxPrice)]);
        if (MinPrice % 1 != 0 && MinPrice.HasValue)
            yield return new ValidationResult(ValidationText.Price, [nameof(MinPrice)]);
        if (MaxPrice % 1 != 0 && MaxPrice.HasValue)
            yield return new ValidationResult(ValidationText.Price, [nameof(MaxPrice)]);
        if (Sort is not ("newest" or "price-asc" or "price-desc" or "title"))
            yield return new ValidationResult(ValidationText.Sort, [nameof(Sort)]);
    }
}

public sealed record HomeViewModel(bool IsDatabaseConfigured, IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<BookCardDto> NewBooks);
public sealed record CatalogViewModel(BookQuery Query, PagedResult<BookCardDto> Books,
    IReadOnlyList<CategoryDto> Categories);
public sealed record DashboardViewModel(int DeliveredOrders, int PendingOrders, int CancelledOrders,
    int Books, int Customers);

public sealed class RegisterViewModel
{
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [RegularExpression(@"^[a-zA-Z0-9_]{3,50}$", ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.UserName))]
    public string UserName { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [EmailAddress(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Email))]
    [StringLength(254, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    public string Email { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [StringLength(64, MinimumLength = 10, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.PasswordLength))]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [Compare(nameof(Password), ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.PasswordMatch))]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class LoginViewModel
{
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [StringLength(50, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    public string UserName { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [StringLength(64, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ProfileViewModel
{
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    [StringLength(100, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    public string? FullName { get; set; }
    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{6,19}$", ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Phone))]
    [StringLength(20, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    public string? PhoneNumber { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [StringLength(64, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.MaxLength))]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [StringLength(64, MinimumLength = 10, ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.PasswordLength))]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";
    [Required(ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.Required))]
    [Compare(nameof(NewPassword), ErrorMessageResourceType = typeof(ValidationText), ErrorMessageResourceName = nameof(ValidationText.PasswordMatch))]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed record ErrorViewModel(int StatusCode, string Code, string Message, string TraceId);
