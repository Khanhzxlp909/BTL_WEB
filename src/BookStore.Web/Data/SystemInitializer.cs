using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Data;

public static class SystemInitializer
{
    // Chỉ được gọi qua lệnh --initialize do người vận hành chủ động chạy.
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        services.GetRequiredService<DatabaseReadiness>().EnsureConfigured();
        var db = services.GetRequiredService<BookStoreDbContext>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SystemInitializer");
        var name = configuration["BootstrapAdmin:UserName"];
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || password is not { Length: >= 10 and <= 64 })
            throw new AppException(400, "ADMIN_CONFIGURATION_INVALID",
                "Không thể khởi tạo quản trị: thiếu BootstrapAdmin__UserName, BootstrapAdmin__Email hoặc mật khẩu 10–64 ký tự. Cấu hình đủ các biến môi trường rồi chạy lại --initialize.");
        var input = new Models.RegisterViewModel { UserName = name, Email = email, Password = password, ConfirmPassword = password };
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input,
            new System.ComponentModel.DataAnnotations.ValidationContext(input), [], true))
            throw new AppException(400, "ADMIN_CONFIGURATION_INVALID", "Thông tin quản trị không đúng BR29. Kiểm tra tên đăng nhập 3–50 ký tự chữ/số/gạch dưới, email hợp lệ, mật khẩu 10–64 ký tự rồi chạy lại --initialize.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var role in new[] { RoleNames.Customer, RoleNames.Admin })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole<int>(role)), "tạo vai trò hệ thống");
        var admin = await users.FindByNameAsync(name);
        if (admin is not null && !await users.IsInRoleAsync(admin, RoleNames.Admin))
            throw new AppException(409, "ADMIN_USERNAME_OCCUPIED", "Không thể khởi tạo quản trị vì tên đăng nhập đã thuộc tài khoản khác. Chọn BootstrapAdmin__UserName khác; hệ thống không tự nâng quyền tài khoản hiện có.");
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = name, Email = email };
            Ensure(await users.CreateAsync(admin, password), "tạo tài khoản quản trị");
            Ensure(await users.AddToRoleAsync(admin, RoleNames.Admin), "gán vai trò quản trị");
        }
        if (!await db.SystemSettings.AnyAsync(x => x.SettingKey == "LowStockThreshold"))
            db.SystemSettings.Add(new SystemSetting { SettingKey = "LowStockThreshold", SettingValue = "5" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Đã khởi tạo vai trò, tài khoản quản trị và ngưỡng tồn thấp. Mật khẩu tài khoản hiện có không bị thay đổi.");
    }

    private static void Ensure(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new AppException(409, "SYSTEM_INITIALIZATION_REJECTED",
            $"Không thể {operation}: {string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Description}"))} Kiểm tra cấu hình rồi chạy lại --initialize.");
    }
}
