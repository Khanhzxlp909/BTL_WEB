using System.Data;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Services;

public sealed class CommunityService(BookStoreDbContext db, DatabaseReadiness readiness,
    UserManager<ApplicationUser> users) : ICommunityService
{
    private async Task<Book> ReviewBook(int userId, int bookId, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var book = await db.Books.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookId, ct) ?? throw AdminErrors.BookMissing(bookId);
        if (!await db.OrderItems.AnyAsync(x => x.BookId == bookId && x.Order.UserId == userId && x.Order.Status == OrderStatus.DELIVERED, ct))
            throw new AppException(403, "REVIEW_DELIVERED_PURCHASE_REQUIRED", ValidationText.Pick(
                "Chưa thể đánh giá sách này vì bạn chưa có đơn đã giao chứa sách. Hãy nhận sách trước rồi quay lại đánh giá.",
                "You have not received this book in a delivered order. Receive the book before reviewing it."));
        return book;
    }

    public async Task<ReviewFormViewModel> ReviewFormAsync(int userId, int bookId, CancellationToken ct)
    {
        var book = await ReviewBook(userId, bookId, ct);
        var review = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(x => x.BookId == bookId && x.UserId == userId, ct);
        return new(bookId, book.Title, review?.IsHidden ?? false,
            new() { Rating = review?.Rating ?? 5, Comment = review?.Comment ?? "" });
    }

    public async Task<bool> SaveReviewAsync(int userId, int bookId, ReviewInput input, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await CommerceSupport.LockUserAsync(db, userId, ct);
        await ReviewBook(userId, bookId, ct);
        var query = db.Database.IsSqlServer()
            ? db.Reviews.FromSqlInterpolated($"SELECT * FROM [Reviews] WITH (UPDLOCK,HOLDLOCK) WHERE [BookId]={bookId} AND [UserId]={userId}")
            : db.Reviews.Where(x => x.BookId == bookId && x.UserId == userId);
        var review = await query.SingleOrDefaultAsync(ct);
        if (review is null) { review = new Review { UserId = userId, BookId = bookId }; db.Reviews.Add(review); }
        review.Rating = input.Rating; review.Comment = input.Comment.Trim(); review.UpdatedAt = DateTime.UtcNow;
        // Chỉ cập nhật nội dung, không ghi đè IsHidden/metadata của thao tác kiểm duyệt.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return review.IsHidden;
    }

    public async Task<ReviewListViewModel> ReviewsAsync(ReviewQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var rows = db.Reviews.AsNoTracking();
        if (query.BookId.HasValue) rows = rows.Where(x => x.BookId == query.BookId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Book.Title.Contains(query.Search.Trim()) || x.User.UserName!.Contains(query.Search.Trim()));
        if (query.Visibility != "all") rows = rows.Where(x => x.IsHidden == (query.Visibility == "hidden"));
        return new(query, await AdminPaging.ReadAsync(rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new AdminReviewDto(x.Id, x.BookId, x.Book.Title, x.User.UserName!, x.Rating, x.Comment, x.IsHidden,
                x.CreatedAt, x.UpdatedAt, x.ModeratedByUser == null ? null : x.ModeratedByUser.UserName, x.ModeratedAt)), query.Page, query.PageSize, ct));
    }

    public async Task ModerateAsync(int actorId, int reviewId, bool hidden, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var query = db.Database.IsSqlServer()
            ? db.Reviews.FromSqlInterpolated($"SELECT * FROM [Reviews] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={reviewId}")
            : db.Reviews.Where(x => x.Id == reviewId);
        var review = await query.SingleOrDefaultAsync(ct) ?? throw new AppException(404, "REVIEW_NOT_FOUND", ValidationText.Pick(
            $"Không tìm thấy đánh giá #{reviewId}. Tải lại danh sách đánh giá.", $"Review #{reviewId} was not found. Reload the review list."));
        if (review.IsHidden != hidden)
        {
            review.IsHidden = hidden; review.ModeratedByUserId = actorId; review.ModeratedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private IQueryable<ApplicationUser> CustomerRows => db.Users.Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id
        && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Customer))
        && !db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Admin)));

    public async Task<CustomerListViewModel> CustomersAsync(CustomerQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var rows = CustomerRows.AsNoTracking();
        if (query.Status != "all") rows = rows.Where(x => x.IsActive == (query.Status == "active"));
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.UserName!.Contains(query.Search.Trim())
            || x.Email!.Contains(query.Search.Trim()) || x.FullName != null && x.FullName.Contains(query.Search.Trim()));
        return new(query, await AdminPaging.ReadAsync(rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new CustomerDto(x.Id, x.UserName!, x.FullName, x.Email!, x.PhoneNumber, x.CreatedAt, x.IsActive, x.LockReason, x.LockedAt)), query.Page, query.PageSize, ct));
    }

    public async Task<CustomerDto> CustomerAsync(int id, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        return await CustomerRows.AsNoTracking().Where(x => x.Id == id).Select(x => new CustomerDto(x.Id, x.UserName!,
            x.FullName, x.Email!, x.PhoneNumber, x.CreatedAt, x.IsActive, x.LockReason, x.LockedAt)).SingleOrDefaultAsync(ct)
            ?? throw CustomerMissing(id);
    }

    public Task LockAsync(int actorId, int id, LockCustomerInput input, CancellationToken ct) => ChangeCustomer(actorId, id, false, input.Reason, ct);
    public Task UnlockAsync(int id, CancellationToken ct) => ChangeCustomer(null, id, true, null, ct);
    private async Task ChangeCustomer(int? actorId, int id, bool active, string? reason, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Lock row kể cả tài khoản đã bị khóa, để serialize khóa/mở và giỏ/đặt đơn.
        var query = db.Database.IsSqlServer()
            ? db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
            : db.Users.Where(x => x.Id == id);
        var user = await query.SingleOrDefaultAsync(ct) ?? throw CustomerMissing(id);
        if (await users.IsInRoleAsync(user, RoleNames.Admin)) throw new AppException(403, "ADMIN_ACCOUNT_LOCK_FORBIDDEN", ValidationText.Pick(
            "Không thể khóa/mở tài khoản Admin qua quản lý khách hàng. Chỉ chọn tài khoản Customer.",
            "Admin accounts cannot be locked or unlocked through customer management. Choose a Customer account."));
        if (!await users.IsInRoleAsync(user, RoleNames.Customer)) throw CustomerMissing(id);
        if (user.IsActive != active)
        {
            user.IsActive = active;
            if (!active) { user.LockReason = reason!.Trim(); user.LockedByUserId = actorId; user.LockedAt = DateTime.UtcNow; }
            else { user.LockReason = null; user.LockedByUserId = null; user.LockedAt = null; user.LockoutEnd = null; user.AccessFailedCount = 0; }
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) throw new AppException(409, "CUSTOMER_UPDATE_CONFLICT", ValidationText.Pick(
                "Không thể cập nhật khóa tài khoản vì dữ liệu xác thực vừa thay đổi. Tải lại danh sách rồi thực hiện lại.",
                "Account authentication data changed during this update. Reload the customer list and try again."));
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<RevenueViewModel> RevenueAsync(ReportQuery query, CancellationToken ct)
    {
        readiness.EnsureConfigured();
        var from = CommerceSupport.StartUtc(query.FromDate);
        var until = CommerceSupport.StartUtc(query.ToDate.AddDays(1));
        var orders = db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.DELIVERED && x.DeliveredAt >= from && x.DeliveredAt < until);
        var revenue = await orders.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var delivered = await orders.CountAsync(ct);
        var cancelled = await db.Orders.CountAsync(x => x.Status == OrderStatus.CANCELLED && x.CancelledAt >= from && x.CancelledAt < until, ct);
        var aggregate = await orders.GroupBy(x => x.DeliveredAt!.Value.AddHours(7).Date)
            .Select(x => new { Day = x.Key, Revenue = x.Sum(y => y.TotalAmount), Count = x.Count() }).ToListAsync(ct);
        var byDate = aggregate.ToDictionary(x => DateOnly.FromDateTime(x.Day));
        var days = new List<DailyRevenueDto>();
        for (var date = query.FromDate; date <= query.ToDate; date = date.AddDays(1))
            days.Add(new(date, byDate.GetValueOrDefault(date)?.Revenue ?? 0, byDate.GetValueOrDefault(date)?.Count ?? 0));
        var top = await db.OrderItems.AsNoTracking().Where(x => x.Order.Status == OrderStatus.DELIVERED
            && x.Order.DeliveredAt >= from && x.Order.DeliveredAt < until)
            .GroupBy(x => x.BookId).Select(x => new { BookId = x.Key, BookTitle = x.Max(y => y.BookTitle)!, SKU = x.Max(y => y.SKU)!,
                Quantity = x.Sum(y => (long)y.Quantity), Revenue = x.Sum(y => y.LineTotal) })
            .OrderByDescending(x => x.Quantity).ThenBy(x => x.BookId).Take(5)
            .Select(x => new BestSellerDto(x.BookId, x.BookTitle, x.SKU, x.Quantity, x.Revenue)).ToListAsync(ct);
        return new(query, revenue, delivered, cancelled, days, top);
    }

    private static AppException CustomerMissing(int id) => new(404, "CUSTOMER_NOT_FOUND", ValidationText.Pick(
        $"Không tìm thấy tài khoản Customer #{id}. Tải lại danh sách khách hàng.", $"Customer #{id} was not found. Reload the customer list."));
}
