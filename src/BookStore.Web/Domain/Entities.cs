using Microsoft.AspNetCore.Identity;

namespace BookStore.Web.Domain;

public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

public enum OrderStatus { PENDING, CONFIRMED, SHIPPING, DELIVERED, CANCELLED }
public enum PaymentStatus { UNPAID, PAID }
public enum InventoryTransactionType { IMPORT, ORDER_PLACED, ORDER_DELIVERED, ORDER_CANCELLED }

public sealed class ApplicationUser : IdentityUser<int>
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? LockReason { get; set; }
    public int? LockedByUserId { get; set; }
    public ApplicationUser? LockedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedAt { get; set; }
}

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Book> Books { get; set; } = [];
}

public sealed class Book
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string? Publisher { get; set; }
    public string? ISBN { get; set; }
    public string SKU { get; set; } = "";
    public int? PublishedYear { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string? Description { get; set; }
    public string? CoverImage { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Inventory Inventory { get; set; } = null!;
    public ICollection<Review> Reviews { get; set; } = [];
}

public sealed class Inventory
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InventoryTransaction
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public int ActorUserId { get; set; }
    public ApplicationUser ActorUser { get; set; } = null!;
    public InventoryTransactionType Type { get; set; }
    public int QuantityDelta { get; set; }
    public int ReservedDelta { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int ReservedBefore { get; set; }
    public int ReservedAfter { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SystemSetting
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = "";
    public string SettingValue { get; set; } = "";
}

public sealed class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CartItem> Items { get; set; } = [];
}

public sealed class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int Quantity { get; set; }
}

public sealed class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string OrderCode { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.PENDING;
    public string PaymentMethod { get; set; } = "COD";
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.UNPAID;
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? Note { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderEvent> Events { get; set; } = [];
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public string BookTitle { get; set; } = "";
    public string SKU { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class OrderEvent
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ActorUserId { get; set; }
    public ApplicationUser ActorUser { get; set; } = null!;
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Review
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public bool IsHidden { get; set; }
    public int? ModeratedByUserId { get; set; }
    public ApplicationUser? ModeratedByUser { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
