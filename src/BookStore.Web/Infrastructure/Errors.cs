using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using BookStore.Web.Models;

namespace BookStore.Web.Infrastructure;

public sealed record AppError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null, string? TraceId = null);

public sealed class AppException(int statusCode, string code, string message,
    IReadOnlyDictionary<string, string[]>? details = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public AppError Error { get; } = new(code, message, details);
}

public static class ErrorResponses
{
    public static bool IsJson(HttpRequest request) => request.Path.StartsWithSegments("/api")
        || request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    public static Dictionary<string, string[]> Details(ModelStateDictionary state) => state
        .Where(x => x.Value?.Errors.Count > 0)
        .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage)
            ? ValidationText.Pick($"Giá trị của {x.Key} không đúng kiểu dữ liệu. Hãy kiểm tra rồi nhập lại.",
                $"The value for {x.Key} has an invalid format. Check and enter it again.") : e.ErrorMessage).ToArray());

    public static AppError ForStatus(int status) => status switch
    {
        400 => new("REQUEST_INVALID", ValidationText.Pick("Không thể xử lý yêu cầu: dữ liệu hoặc mã chống giả mạo không hợp lệ. Tải lại trang rồi gửi lại.", "The request data or anti-forgery token is invalid. Reload the page and try again.")),
        401 => new("AUTHENTICATION_REQUIRED", ValidationText.Pick("Phiên đăng nhập chưa có, đã hết hạn hoặc tài khoản bị khóa. Hãy đăng nhập lại; liên hệ quản trị viên nếu vẫn bị từ chối.", "Your session is missing, expired or your account is locked. Sign in again; contact an administrator if access is still denied.")),
        403 => new("PERMISSION_DENIED", ValidationText.Pick("Tài khoản không có quyền thực hiện thao tác này. Hãy đăng nhập tài khoản có vai trò phù hợp.", "Your account does not have permission for this operation. Sign in with an authorized account.")),
        404 => new("RESOURCE_NOT_FOUND", ValidationText.Pick("Không tìm thấy trang hoặc dữ liệu được yêu cầu. Kiểm tra địa chỉ hoặc quay về danh sách sách.", "The requested page or resource could not be found. Check the address or return to the catalog.")),
        405 => new("METHOD_NOT_ALLOWED", ValidationText.Pick("Phương thức gửi không được hỗ trợ tại địa chỉ này. Hãy sử dụng biểu mẫu hoặc API đúng tài liệu.", "This endpoint does not support that HTTP method. Use the documented form or API method.")),
        413 => new("UPLOAD_REQUEST_TOO_LARGE", ValidationText.Pick("Không thể gửi biểu mẫu tải ảnh vì toàn bộ yêu cầu vượt giới hạn máy chủ. Chọn một ảnh JPEG, PNG hoặc WEBP không quá 5 MB rồi gửi lại.", "The upload form exceeds the server request limit. Choose one JPEG, PNG or WEBP image of at most 5 MB and submit again.")),
        _ => new("REQUEST_REJECTED", ValidationText.Pick($"Yêu cầu bị từ chối với trạng thái {status}. Kiểm tra dữ liệu, quyền truy cập rồi thử lại.", $"The request was rejected with status {status}. Check your input and permissions, then try again."))
    };

    public static async Task WriteAsync(HttpContext context, int status, AppError error)
    {
        context.Response.StatusCode = status;
        error = error with { TraceId = error.TraceId ?? context.TraceIdentifier };
        if (IsJson(context.Request))
        {
            await context.Response.WriteAsJsonAsync(error);
            return;
        }
        var result = new ViewResult
        {
            ViewName = "~/Views/Shared/Error.cshtml",
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<ErrorViewModel>(
                new EmptyModelMetadataProvider(), new ModelStateDictionary())
            { Model = new(status, error.Code, error.Message, error.TraceId!) }
        };
        await result.ExecuteResultAsync(new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
    }
}

public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (AppException ex) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            await ErrorResponses.WriteAsync(context, ex.StatusCode, ex.Error);
        }
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            // Không ghi request body, query, connection string hay thông báo DB có thể chứa dữ liệu cá nhân.
            var database = ex is DbException || ex.InnerException is DbException;
            var operation = context.GetRouteValue("controller")?.ToString() switch
            {
                "Home" => ValidationText.Pick("tải dữ liệu trang chủ", "load home page data"),
                "Catalog" or "CatalogApi" => ValidationText.Pick("tra cứu sách và thể loại", "query books and categories"),
                "Account" => ValidationText.Pick("xử lý tài khoản", "process the account operation"),
                "Admin" => ValidationText.Pick("tổng hợp số liệu quản trị", "aggregate administration statistics"),
                "AdminBooks" or "AdminBooksApi" => ValidationText.Pick("quản trị thông tin sách và ảnh bìa", "manage book details and cover images"),
                "AdminCategories" => ValidationText.Pick("quản trị thể loại sách", "manage book categories"),
                "Inventory" => ValidationText.Pick("cập nhật hoặc tra cứu kho sách", "update or query book inventory"),
                "Cart" or "CartApi" => ValidationText.Pick("cập nhật hoặc đọc giỏ hàng", "update or read the shopping cart"),
                "Checkout" or "Orders" or "OrdersApi" or "AdminOrders" or "AdminOrdersApi" => ValidationText.Pick("đặt hàng hoặc xử lý trạng thái đơn COD", "place or process a COD order"),
                "Reviews" or "AdminReviews" => ValidationText.Pick("lưu hoặc kiểm duyệt đánh giá sách", "save or moderate a book review"),
                "Customers" => ValidationText.Pick("tra cứu hoặc khóa/mở tài khoản khách hàng", "query or lock/unlock customer accounts"),
                "Reports" => ValidationText.Pick("tổng hợp báo cáo doanh thu theo ngày Việt Nam", "aggregate revenue by Vietnamese calendar date"),
                "Language" => ValidationText.Pick("lưu lựa chọn ngôn ngữ", "save the selected language"),
                _ => ValidationText.Pick("xử lý địa chỉ được yêu cầu", "process the requested endpoint")
            };
            var sql = ex as Microsoft.Data.SqlClient.SqlException ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
            var reason = sql?.Number switch
            {
                18456 => ValidationText.Pick("SQL Server từ chối thông tin xác thực", "SQL Server rejected the database credentials"),
                4060 => ValidationText.Pick("SQL Server không mở được cơ sở dữ liệu đã cấu hình", "SQL Server could not open the configured database"),
                208 => ValidationText.Pick("schema cơ sở dữ liệu chưa có bảng cần truy cập", "the database schema is missing a required table"),
                -2 => ValidationText.Pick("thao tác SQL Server vượt thời gian chờ", "the SQL Server operation timed out"),
                _ when database => ValidationText.Pick("kết nối hoặc thao tác cơ sở dữ liệu bị từ chối", "the database connection or operation was rejected"),
                _ => ValidationText.Pick("thành phần xử lý phát sinh ngoại lệ ngoài dự kiến", "the processing component raised an unexpected exception")
            };
            FailureDiagnostics.Log(logger, ex, context.TraceIdentifier,
                $"{context.Request.Method} {context.GetEndpoint()?.DisplayName ?? "unmatched endpoint"}");
            context.Response.Clear();
            await ErrorResponses.WriteAsync(context, database ? 503 : 500, new(
                database ? "DATABASE_OPERATION_FAILED" : "OPERATION_FAILED",
                ValidationText.Pick($"Không thể {operation} vì {reason}. Thử lại; gửi mã tra cứu cho quản trị viên để kiểm tra bước này.",
                    $"Could not {operation} because {reason}. Try again; give the reference ID to an administrator to investigate this step.")));
        }
    }
}

public static class FailureDiagnostics
{
    public static void Log(ILogger logger, Exception exception, string reference, string operation)
    {
        var causes = new List<object>();
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
            causes.Add(new
            {
                Type = cause.GetType().FullName,
                cause.HResult,
                DatabaseErrorNumber = (cause as Microsoft.Data.SqlClient.SqlException)?.Number,
                Stack = new System.Diagnostics.StackTrace(cause, true).ToString()
            });
        logger.LogError("BookStore operation failed. TraceId={TraceId}; Operation={Operation}; Causes={Causes}",
            reference, operation, JsonSerializer.Serialize(causes));
    }
}

public sealed class AntiforgeryErrorFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;
        var error = new AppError("ANTIFORGERY_TOKEN_INVALID", ValidationText.Pick(
            "Không thể gửi biểu mẫu vì mã chống giả mạo bị thiếu hoặc hết hiệu lực. Tải lại trang rồi gửi lại.",
            "The form anti-forgery token is missing or invalid. Reload the page and submit again."), TraceId: context.HttpContext.TraceIdentifier);
        context.Result = ErrorResponses.IsJson(context.HttpContext.Request)
            ? new ObjectResult(error) { StatusCode = 400 }
            : new ViewResult { ViewName = "~/Views/Shared/Error.cshtml", StatusCode = 400,
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<ErrorViewModel>(
                    new EmptyModelMetadataProvider(), context.ModelState)
                { Model = new(400, error.Code, error.Message, error.TraceId!) } };
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}

public sealed class ValidationErrorFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid || !ErrorResponses.IsJson(context.HttpContext.Request)) return;
        context.Result = new BadRequestObjectResult(new AppError("VALIDATION_FAILED",
            ValidationText.Pick("Dữ liệu gửi không hợp lệ. Hãy sửa các trường được chỉ rõ rồi gửi lại.", "The submitted data is invalid. Correct the listed fields and try again."),
            ErrorResponses.Details(context.ModelState), context.HttpContext.TraceIdentifier));
    }
    public void OnActionExecuted(ActionExecutedContext context) { }
}
