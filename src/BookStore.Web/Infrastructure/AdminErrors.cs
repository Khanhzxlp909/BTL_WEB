using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Infrastructure;

public static class AdminErrors
{
    public static AppException Field(string code, string field, string message, int status = 400)
        => new(status, code, message, new Dictionary<string, string[]> { [field] = [message] });

    public static void Apply(Controller controller, AppException exception, string prefix = "")
    {
        controller.Response.StatusCode = exception.StatusCode;
        controller.ViewData["ErrorCode"] = exception.Error.Code;
        controller.ViewData["TraceId"] = controller.HttpContext.TraceIdentifier;
        if (exception.Error.Details is { Count: > 0 } details)
            foreach (var (field, messages) in details)
                foreach (var message in messages) controller.ModelState.AddModelError(prefix + field, message);
        else controller.ModelState.AddModelError("", exception.Message);
    }

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.GetType().Name == "SqliteException"
        && exception.InnerException.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal);

    public static AppException BookMissing(int id) => new(404, "ADMIN_BOOK_NOT_FOUND", ValidationText.Pick(
        $"Không tìm thấy sách #{id}. Tải lại danh sách sách quản trị rồi chọn sách còn tồn tại.",
        $"Book #{id} could not be found. Reload the administration book list and choose an existing book."));
    public static AppException CategoryMissing(int id) => new(404, "CATEGORY_NOT_FOUND", ValidationText.Pick(
        $"Không tìm thấy thể loại #{id} đang hoạt động. Chọn thể loại khác hoặc tải lại danh sách.",
        $"Active category #{id} could not be found. Select another category or reload the list."));
}
