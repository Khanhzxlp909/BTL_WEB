using System.ComponentModel.DataAnnotations;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Models;

public sealed class ReviewInput : IValidatableObject
{
    public int Rating { get; set; } = 5;
    [FieldRequired, TextLimit(1000)] public string Comment { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Rating is < 1 or > 5) yield return new(ValidationText.Pick("Điểm đánh giá phải từ 1 đến 5 sao. Hãy chọn lại điểm.", "Choose a rating between 1 and 5 stars."), [nameof(Rating)]);
    }
}
public sealed record ReviewFormViewModel(int BookId, string BookTitle, bool IsHidden, ReviewInput Input);
public sealed class ReviewQuery : AdminPageQuery, IValidatableObject
{
    public int? BookId { get; set; }
    public string Visibility { get; set; } = "all";
    public new IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var result in base.Validate(context)) yield return result;
        if (BookId <= 0) yield return new(ValidationText.Pick("Mã sách phải là số nguyên dương. Hãy chọn lại sách.", "Choose a positive book ID."), [nameof(BookId)]);
        if (Visibility is not ("all" or "visible" or "hidden")) yield return new(ValidationText.Pick("Bộ lọc đánh giá phải là all, visible hoặc hidden.", "Choose all, visible or hidden reviews."), [nameof(Visibility)]);
    }
}
public sealed record AdminReviewDto(int Id, int BookId, string BookTitle, string UserName, int Rating,
    string Comment, bool IsHidden, DateTime CreatedAt, DateTime UpdatedAt, string? ModeratedBy, DateTime? ModeratedAt);
public sealed record ReviewListViewModel(ReviewQuery Query, PagedResult<AdminReviewDto> Reviews);
public sealed class CustomerQuery : AdminPageQuery, IValidatableObject
{
    public string Status { get; set; } = "all";
    public new IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var result in base.Validate(context)) yield return result;
        if (Status is not ("all" or "active" or "locked")) yield return new(ValidationText.Pick("Bộ lọc khách phải là all, active hoặc locked.", "Choose all, active or locked customers."), [nameof(Status)]);
    }
}
public sealed class LockCustomerInput { [FieldRequired, TextLimit(500)] public string Reason { get; set; } = ""; }
public sealed record CustomerDto(int Id, string UserName, string? FullName, string Email, string? Phone,
    DateTime CreatedAt, bool IsActive, string? LockReason, DateTime? LockedAt);
public sealed record CustomerListViewModel(CustomerQuery Query, PagedResult<CustomerDto> Customers);
public sealed record LockCustomerViewModel(CustomerDto Customer, LockCustomerInput Input);
public sealed class ReportQuery : IValidatableObject
{
    public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-29);
    public DateOnly ToDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FromDate > ToDate || ToDate == DateOnly.MaxValue || FromDate == DateOnly.MinValue)
            yield return new(ValidationText.Pick("Khoảng báo cáo không hợp lệ. Ngày bắt đầu không sau ngày kết thúc; chọn ngày từ 02/01/0001 đến 30/12/9999.", "Choose a valid report range from 0001-01-02 to 9999-12-30, with start no later than end."), [nameof(ToDate)]);
    }
}
public sealed record DailyRevenueDto(DateOnly Date, decimal Revenue, int DeliveredOrders);
public sealed record BestSellerDto(int BookId, string BookTitle, string SKU, long Quantity, decimal Revenue);
public sealed record RevenueViewModel(ReportQuery Query, decimal TotalRevenue, int DeliveredOrders,
    int CancelledOrders, IReadOnlyList<DailyRevenueDto> Days, IReadOnlyList<BestSellerDto> BestSellers);
public interface ICommunityService
{
    Task<ReviewFormViewModel> ReviewFormAsync(int userId, int bookId, CancellationToken ct);
    Task<bool> SaveReviewAsync(int userId, int bookId, ReviewInput input, CancellationToken ct);
    Task<ReviewListViewModel> ReviewsAsync(ReviewQuery query, CancellationToken ct);
    Task ModerateAsync(int actorId, int reviewId, bool hidden, CancellationToken ct);
    Task<CustomerListViewModel> CustomersAsync(CustomerQuery query, CancellationToken ct);
    Task<CustomerDto> CustomerAsync(int id, CancellationToken ct);
    Task LockAsync(int actorId, int id, LockCustomerInput input, CancellationToken ct);
    Task UnlockAsync(int id, CancellationToken ct);
    Task<RevenueViewModel> RevenueAsync(ReportQuery query, CancellationToken ct);
}
