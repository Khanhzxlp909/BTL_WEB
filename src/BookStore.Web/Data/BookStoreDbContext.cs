using BookStore.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BookStore.Web.Data;

public class BookStoreDbContext(DbContextOptions<BookStoreDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            e.Property(x => x.NormalizedUserName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.LockReason).HasMaxLength(500);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasOne(x => x.LockedByUser).WithMany().HasForeignKey(x => x.LockedByUserId);
            e.ToTable("AspNetUsers", t => t.HasCheckConstraint("CK_Users_Lock",
                "[IsActive] = 1 OR ([LockReason] IS NOT NULL AND [LockReason] <> '' AND [LockedByUserId] IS NOT NULL AND [LockedAt] IS NOT NULL)"));
        });
        b.Entity<IdentityRole<int>>().Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Entity<IdentityRole<int>>().Property(x => x.NormalizedName).HasMaxLength(50).IsRequired();
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.Name).IsUnique();
        });
        b.Entity<Book>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.Property(x => x.Author).HasMaxLength(150).IsRequired();
            e.Property(x => x.Publisher).HasMaxLength(150);
            e.Property(x => x.ISBN).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.SKU).HasMaxLength(64).IsUnicode(false).IsRequired();
            e.Property(x => x.CoverImage).HasMaxLength(500);
            e.HasIndex(x => x.SKU).IsUnique();
            e.HasIndex(x => new { x.IsDeleted, x.CreatedAt });
            e.HasOne(x => x.Category).WithMany(x => x.Books).HasForeignKey(x => x.CategoryId);
            e.ToTable("Books", t =>
            {
                t.HasCheckConstraint("CK_Books_Price", "[Price] > 0 AND ([DiscountPrice] IS NULL OR ([DiscountPrice] > 0 AND [DiscountPrice] <= [Price]))");
                t.HasCheckConstraint("CK_Books_Year", "[PublishedYear] IS NULL OR [PublishedYear] >= 1");
            });
        });
        b.Entity<Inventory>(e =>
        {
            e.HasOne(x => x.Book).WithOne(x => x.Inventory).HasForeignKey<Inventory>(x => x.BookId);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable("Inventories", t => t.HasCheckConstraint("CK_Inventories_Quantity",
                "[Quantity] >= 0 AND [ReservedQuantity] >= 0 AND [ReservedQuantity] <= [Quantity]"));
        });
        b.Entity<InventoryTransaction>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.Reference).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Inventory).WithMany().HasForeignKey(x => x.InventoryId);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId);
            e.HasIndex(x => new { x.InventoryId, x.CreatedAt });
            e.ToTable("InventoryTransactions", t =>
            {
                t.HasCheckConstraint("CK_InventoryTransactions_Type", "[Type] IN ('IMPORT','ORDER_PLACED','ORDER_DELIVERED','ORDER_CANCELLED')");
                t.HasCheckConstraint("CK_InventoryTransactions_Import", "[Type] <> 'IMPORT' OR ([QuantityDelta] > 0 AND [Reference] IS NOT NULL AND [Reference] <> '' AND [Reason] IS NOT NULL AND [Reason] <> '')");
                t.HasCheckConstraint("CK_InventoryTransactions_Balance", "[QuantityAfter] = [QuantityBefore] + [QuantityDelta] AND [ReservedAfter] = [ReservedBefore] + [ReservedDelta] AND [QuantityBefore] >= 0 AND [QuantityAfter] >= 0 AND [ReservedBefore] >= 0 AND [ReservedAfter] >= 0 AND [ReservedBefore] <= [QuantityBefore] AND [ReservedAfter] <= [QuantityAfter]");
            });
        });
        b.Entity<SystemSetting>(e =>
        {
            e.Property(x => x.SettingKey).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.SettingValue).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.SettingKey).IsUnique();
        });
        b.Entity<Cart>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasIndex(x => x.UserId).IsUnique();
        });
        b.Entity<CartItem>(e =>
        {
            e.HasOne(x => x.Cart).WithMany(x => x.Items).HasForeignKey(x => x.CartId);
            e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId);
            e.HasIndex(x => new { x.CartId, x.BookId }).IsUnique();
            e.ToTable("CartItems", t => t.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] > 0"));
        });
        b.Entity<Order>(e =>
        {
            e.Property(x => x.OrderCode).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.PaymentMethod).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.RecipientName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.ShippingAddress).HasMaxLength(255).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.OrderCode).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.DeliveredAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.ToTable("Orders", t =>
            {
                t.HasCheckConstraint("CK_Orders_Status", "[Status] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED')");
                t.HasCheckConstraint("CK_Orders_Total", "[Subtotal] >= 0 AND [ShippingFee] = 0 AND [TotalAmount] = [Subtotal] + [ShippingFee]");
                t.HasCheckConstraint("CK_Orders_Payment", "[PaymentMethod] = 'COD' AND (([Status] = 'DELIVERED' AND [PaymentStatus] = 'PAID' AND [DeliveredAt] IS NOT NULL) OR ([Status] <> 'DELIVERED' AND [PaymentStatus] = 'UNPAID' AND [DeliveredAt] IS NULL))");
                t.HasCheckConstraint("CK_Orders_Cancelled", "([Status] = 'CANCELLED' AND [CancelledAt] IS NOT NULL) OR ([Status] <> 'CANCELLED' AND [CancelledAt] IS NULL)");
            });
        });
        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.BookTitle).HasMaxLength(255).IsRequired();
            e.Property(x => x.SKU).HasMaxLength(64).IsUnicode(false).IsRequired();
            e.Property(x => x.LineTotal).HasComputedColumnSql("[Quantity] * [UnitPrice]", stored: true);
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId);
            e.HasIndex(x => new { x.OrderId, x.BookId }).IsUnique();
            e.ToTable("OrderItems", t => t.HasCheckConstraint("CK_OrderItems_Value", "[Quantity] > 0 AND [UnitPrice] > 0"));
        });
        b.Entity<OrderEvent>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Order).WithMany(x => x.Events).HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId);
            e.HasIndex(x => new { x.OrderId, x.CreatedAt });
            e.ToTable("OrderEvents", t => t.HasCheckConstraint("CK_OrderEvents_Status",
                "[ToStatus] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED') AND ([FromStatus] IS NULL OR [FromStatus] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED'))"));
        });
        b.Entity<Review>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Book).WithMany(x => x.Reviews).HasForeignKey(x => x.BookId);
            e.HasOne(x => x.ModeratedByUser).WithMany().HasForeignKey(x => x.ModeratedByUserId);
            e.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
            e.HasIndex(x => new { x.BookId, x.IsHidden, x.CreatedAt });
            e.ToTable("Reviews", t =>
            {
                t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5 AND [Comment] <> ''");
                t.HasCheckConstraint("CK_Reviews_Moderation", "[IsHidden] = 0 OR ([ModeratedByUserId] IS NOT NULL AND [ModeratedAt] IS NOT NULL)");
            });
        });
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var fk in entity.GetForeignKeys()) fk.DeleteBehavior = DeleteBehavior.Restrict;
            foreach (var p in entity.GetProperties())
            {
                if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                {
                    p.SetPrecision(18);
                    p.SetScale(0);
                }
                if (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                {
                    p.SetColumnType("datetime2");
                    p.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                        value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc)));
                }
            }
        }
    }

    private void ProtectHistory()
    {
        if (ChangeTracker.Entries().Any(e => e.Entity is InventoryTransaction or OrderEvent
            && e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Lịch sử kho và trạng thái đơn chỉ được thêm. Hãy tạo sự kiện mới thay vì sửa hoặc xóa lịch sử.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
