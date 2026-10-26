using System.Globalization;
using System.Text.Json.Serialization;
using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Services;
using BookStore.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BookStoreDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("BookStore") ?? ""));
builder.Services.AddSingleton<DatabaseReadiness>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICoverImageStorage, CoverImageStorage>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<BookStoreDbContext>()
    .AddSignInManager<ActiveUserSignInManager>()
    .AddPasswordValidator<BookStorePasswordValidator>()
    .AddErrorDescriber<BookStoreIdentityErrors>()
    .AddDefaultTokenProviders();
// Kiểm tra security stamp mỗi request để khóa tài khoản/đổi mật khẩu vô hiệu hóa ngay phiên cũ.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "BookStore.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/tai-khoan/dang-nhap";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (ErrorResponses.IsJson(context.Request))
            return ErrorResponses.WriteAsync(context.HttpContext, 401, ErrorResponses.ForStatus(401));
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
        ErrorResponses.WriteAsync(context.HttpContext, 403, ErrorResponses.ForStatus(403));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "BookStore.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo("vi"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<AntiforgeryErrorFilter>();
    options.Filters.Add<ValidationErrorFilter>();
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, field) =>
        ValidationText.Pick($"Trường {field} không đúng kiểu dữ liệu. Hãy kiểm tra giá trị rồi nhập lại.", $"{field} has an invalid format. Check the value and try again."));
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(field =>
        ValidationText.Pick($"Trường {field} phải là số. Hãy nhập lại giá trị hợp lệ.", $"{field} must be a number. Enter a valid value."));
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => ValidationText.Required);
}).AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(new AppError("VALIDATION_FAILED", ValidationText.Pick(
        "Dữ liệu yêu cầu không hợp lệ. Hãy sửa các trường được chỉ rõ rồi gửi lại.",
        "The request data is invalid. Correct the listed fields and try again."),
        ErrorResponses.Details(context.ModelState), context.HttpContext.TraceIdentifier)));

var app = builder.Build();
if (args.Contains("--initialize", StringComparer.Ordinal))
{
    CultureInfo.CurrentUICulture = new CultureInfo("vi");
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        await SystemInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration);
    }
    catch (AppException ex)
    {
        app.Logger.LogError("{Code}: {Message}", ex.Error.Code, ex.Message);
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        var reference = Guid.NewGuid().ToString("N");
        FailureDiagnostics.Log(app.Logger, ex, reference, "System initialization");
        app.Logger.LogError("SYSTEM_INITIALIZATION_FAILED: Không thể hoàn tất khởi tạo vai trò, quản trị và ngưỡng tồn. Kiểm tra kết nối SQL Server, schema và cấu hình BootstrapAdmin rồi chạy lại --initialize. Mã tra cứu: {TraceId}", reference);
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
app.UseRequestLocalization();
app.UseMiddleware<ErrorMiddleware>();
app.UseStatusCodePages(async context => await ErrorResponses.WriteAsync(context.HttpContext,
    context.HttpContext.Response.StatusCode, ErrorResponses.ForStatus(context.HttpContext.Response.StatusCode)));
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next(context);
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
