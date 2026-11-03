using System.ComponentModel.DataAnnotations;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Models;

public sealed class CartAddInput
{
    [PositiveInteger] public int BookId { get; set; }
    [PositiveInteger] public int Quantity { get; set; } = 1;
}
public sealed class CartQuantityInput : IValidatableObject
{
    public int Quantity { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Quantity < 0) yield return new(ValidationText.Pick("Số lượng giỏ phải là số nguyên không âm; 0 để xóa dòng. Hãy nhập lại.", "Use a non-negative whole quantity; 0 removes the item."), [nameof(Quantity)]);
    }
}
public sealed record CartLineDto(int Id, int BookId, string Title, string SKU, string? CoverImage,
    int Quantity, decimal UnitPrice, int AvailableQuantity, bool IsAvailable, string? Warning, bool PriceChanged = false)
{
    public decimal LineTotal => Quantity * UnitPrice;
    public string UnitPriceText => UnitPrice.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LineTotalText => LineTotal.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}
public sealed record CartDto(IReadOnlyList<CartLineDto> Items)
{
    public long ItemCount => Items.Sum(x => (long)x.Quantity);
    public decimal Subtotal => Items.Sum(x => x.LineTotal);
    public decimal ShippingFee => 0;
    public decimal TotalAmount => Subtotal;
    public string SubtotalText => Subtotal.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TotalAmountText => SubtotalText;
    public bool CanCheckout => Items.Count > 0 && Items.All(x => x.IsAvailable) && TotalAmount <= 999999999999999999m;
}
public sealed class ConfirmTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
    public override string FormatErrorMessage(string name) => ValidationText.Pick("Hãy xác nhận báo giá và thanh toán COD trước khi đặt hàng.", "Confirm the quotation and COD payment before placing the order.");
}
public sealed class PhoneInputAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text && System.Text.RegularExpressions.Regex.IsMatch(text, @"^\+?[0-9][0-9 ()\-]{6,19}$");
    public override string FormatErrorMessage(string name) => ValidationText.Phone;
}
public sealed class CheckoutInput
{
    [FieldRequired, TextLimit(100)] public string RecipientName { get; set; } = "";
    [FieldRequired, TextLimit(20), PhoneInput] public string Phone { get; set; } = "";
    [FieldRequired, TextLimit(255)] public string ShippingAddress { get; set; } = "";
    [TextLimit(500)] public string? Note { get; set; }
    [FieldRequired] public string QuoteToken { get; set; } = "";
    [ConfirmTrue] public bool ConfirmQuote { get; set; }
}
public sealed record CheckoutViewModel(CartDto Cart, CheckoutInput Input);
public sealed record OrderPlacedViewModel(int Id, string OrderCode, decimal TotalAmount, string PaymentMethod, DateTime CreatedAt)
{
    public string TotalAmountText => TotalAmount.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}
public sealed class OrderQuery : AdminPageQuery, IValidatableObject
{
    public OrderStatus? Status { get; set; }
    [TextLimit(100)] public string? Customer { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public new IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var result in base.Validate(context)) yield return result;
        if (Status.HasValue && !Enum.IsDefined(Status.Value)) yield return new(ValidationText.Pick("Trạng thái đơn không hợp lệ. Chọn một trong năm trạng thái đơn.", "Select one of the five valid order statuses."), [nameof(Status)]);
        if (FromDate > ToDate || ToDate == DateOnly.MaxValue || FromDate == DateOnly.MinValue)
            yield return new(ValidationText.Pick("Khoảng ngày đơn không hợp lệ. Ngày đầu không sau ngày cuối; ngày cuối phải nhỏ hơn 31/12/9999.", "Choose a valid date range; start cannot follow end, and end must be before 9999-12-31."), [nameof(ToDate)]);
    }
}
public sealed record OrderSummaryDto(int Id, string OrderCode, string UserName, DateTime CreatedAt,
    decimal TotalAmount, OrderStatus Status, PaymentStatus PaymentStatus);
public sealed record OrderListViewModel(OrderQuery Query, PagedResult<OrderSummaryDto> Orders, bool IsAdmin);
public sealed record OrderLineDto(int BookId, string BookTitle, string SKU, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record OrderEventDto(OrderStatus? FromStatus, OrderStatus ToStatus, string ActorUserName, string? Note, DateTime CreatedAt);
public sealed record OrderDetailDto(int Id, string OrderCode, string UserName, OrderStatus Status,
    PaymentStatus PaymentStatus, string PaymentMethod, string RecipientName, string Phone, string ShippingAddress,
    string? Note, decimal Subtotal, decimal ShippingFee, decimal TotalAmount, DateTime CreatedAt,
    DateTime? DeliveredAt, DateTime? CancelledAt, IReadOnlyList<OrderLineDto> Items, IReadOnlyList<OrderEventDto> Events)
{
    public bool CanCancel => Status is OrderStatus.PENDING or OrderStatus.CONFIRMED;
    public OrderStatus? NextStatus => Status switch
    {
        OrderStatus.PENDING => OrderStatus.CONFIRMED,
        OrderStatus.CONFIRMED => OrderStatus.SHIPPING,
        OrderStatus.SHIPPING => OrderStatus.DELIVERED,
        _ => null
    };
}
public sealed record OrderDetailViewModel(OrderDetailDto Order, bool IsAdmin);
public sealed class CancelOrderInput { [TextLimit(500)] public string? Reason { get; set; } }
public sealed class OrderStatusInput : IValidatableObject
{
    public OrderStatus Status { get; set; }
    public bool ConfirmCodCollected { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Enum.IsDefined(Status)) yield return new(ValidationText.Pick("Trạng thái đơn không hợp lệ. Chọn bước xử lý đúng trên chi tiết đơn.", "Select a valid next step from the order detail page."), [nameof(Status)]);
    }
}
public interface ICartService
{
    Task<CartDto> GetAsync(int userId, CancellationToken ct);
    Task<CartDto> AddAsync(int userId, CartAddInput input, CancellationToken ct);
    Task<CartDto> UpdateAsync(int userId, int itemId, int quantity, CancellationToken ct);
    Task<CartDto> ClearAsync(int userId, CancellationToken ct);
}
public interface IOrderService
{
    Task<CheckoutViewModel> CheckoutAsync(int userId, CancellationToken ct);
    Task<OrderDetailDto> PlaceAsync(int userId, CheckoutInput input, CancellationToken ct);
    Task<OrderListViewModel> ListAsync(int userId, bool admin, OrderQuery query, CancellationToken ct);
    Task<OrderDetailDto> DetailAsync(int userId, bool admin, int id, CancellationToken ct);
    Task<int> ResolveCodeAsync(int userId, string orderCode, CancellationToken ct);
    Task CancelAsync(int userId, bool admin, int id, CancelOrderInput input, CancellationToken ct);
    Task TransitionAsync(int actorId, int id, OrderStatusInput input, CancellationToken ct);
}
