using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Web.Controllers;

[Route("tai-khoan")]
public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    RoleManager<IdentityRole<int>> roles, BookStoreDbContext db, DatabaseReadiness readiness) : Controller
{
    [HttpGet("dang-ky")]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("dang-ky")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return InvalidForm(model);
        readiness.EnsureConfigured();
        if (!await roles.RoleExistsAsync(RoleNames.Customer))
            throw new AppException(503, "CUSTOMER_ROLE_NOT_INITIALIZED", ValidationText.Pick(
                "Chưa thể đăng ký vì vai trò Customer chưa được khởi tạo. Quản trị viên cần chạy bước khởi tạo hệ thống trong README.md.",
                "Registration is unavailable because the Customer role is not initialized. An administrator must follow the initialization steps in README.md."));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = model.UserName, Email = model.Email.Trim() };
        try
        {
            var result = await users.CreateAsync(user, model.Password);
            if (!result.Succeeded) { AddIdentityErrors(result); return InvalidForm(model); }
            result = await users.AddToRoleAsync(user, RoleNames.Customer);
            if (!result.Succeeded) { AddIdentityErrors(result); return InvalidForm(model); }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError("", ValidationText.Pick("Tên đăng nhập hoặc email vừa được đăng ký bởi yêu cầu khác. Chọn thông tin khác hoặc đăng nhập.", "Another request just registered this username or email. Choose another or sign in."));
            return InvalidForm(model, 409);
        }
        TempData["Success"] = ValidationText.Pick("Đã tạo tài khoản Customer. Hãy đăng nhập để sử dụng.", "Your Customer account has been created. Sign in to continue.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("dang-nhap")]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("dang-nhap")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return InvalidForm(model);
        readiness.EnsureConfigured();
        var result = await signIn.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);
        // Identity kiểm tra CanSignIn trước mật khẩu: chỉ tiết lộ khóa sau khi xác minh mật khẩu.
        var blockedUser = result.IsNotAllowed ? await users.FindByNameAsync(model.UserName) : null;
        if (blockedUser is { IsActive: false } && await users.CheckPasswordAsync(blockedUser, model.Password))
        {
            ModelState.AddModelError("", ValidationText.Pick(
                "Tài khoản đã bị quản trị viên khóa nên không thể đăng nhập. Liên hệ quản trị viên nhà sách để được xem xét mở khóa.",
                "This account has been locked by an administrator. Contact the bookstore administrator to request unlocking."));
            ViewData["ErrorCode"] = "ACCOUNT_LOCKED";
            return InvalidForm(model, 403);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", ValidationText.Pick(
                "Không thể đăng nhập: thông tin xác thực không đúng hoặc tài khoản đang bị khóa. Kiểm tra tên đăng nhập, mật khẩu; thử lại sau nếu nhập sai nhiều lần, hoặc liên hệ quản trị viên.",
                "Sign-in failed: the credentials are incorrect or the account is locked. Check your username and password; try later after repeated failures, or contact an administrator."));
            return InvalidForm(model, 401);
        }
        if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
        var user = await users.FindByNameAsync(model.UserName);
        return await users.IsInRoleAsync(user!, RoleNames.Admin) ? Redirect("/admin") : Redirect("/");
    }

    [Authorize, HttpPost("dang-xuat")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        HttpContext.Session.Clear();
        return Redirect("/");
    }

    [Authorize, HttpGet("ho-so")]
    public async Task<IActionResult> Profile()
    {
        var user = await CurrentUser();
        return View(new ProfileViewModel { UserName = user.UserName!, Email = user.Email!, FullName = user.FullName, PhoneNumber = user.PhoneNumber });
    }

    [Authorize, HttpPost("ho-so")]
    public async Task<IActionResult> Profile([Bind("FullName,PhoneNumber")] ProfileViewModel model)
    {
        var user = await CurrentUser();
        model.UserName = user.UserName!;
        model.Email = user.Email!;
        if (!ModelState.IsValid) return InvalidForm(model);
        user.FullName = model.FullName?.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) { AddIdentityErrors(result); return InvalidForm(model); }
        TempData["Success"] = ValidationText.Pick("Đã cập nhật hồ sơ cá nhân.", "Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpGet("doi-mat-khau")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize, HttpPost("doi-mat-khau")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return InvalidForm(model);
        var result = await users.ChangePasswordAsync(await CurrentUser(), model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded) { AddIdentityErrors(result); return InvalidForm(model); }
        await signIn.SignOutAsync();
        HttpContext.Session.Clear();
        TempData["Success"] = ValidationText.Pick("Đã đổi mật khẩu và kết thúc các phiên cũ. Hãy đăng nhập lại.", "Your password has changed and previous sessions are invalid. Sign in again.");
        return RedirectToAction(nameof(Login));
    }

    private async Task<ApplicationUser> CurrentUser()
    {
        readiness.EnsureConfigured();
        return await users.GetUserAsync(User) ?? throw new AppException(401, "SESSION_USER_UNAVAILABLE", ValidationText.Pick("Không thể tải tài khoản của phiên hiện tại. Hãy đăng nhập lại.", "The account for this session is unavailable. Sign in again."));
    }

    private ViewResult InvalidForm(object model, int status = 400)
    {
        Response.StatusCode = status;
        // Không render lại mật khẩu thông qua ModelState hoặc thuộc tính model.
        foreach (var key in new[] { "Password", "ConfirmPassword", "CurrentPassword", "NewPassword" })
            if (ModelState.TryGetValue(key, out var entry)) entry.RawValue = entry.AttemptedValue = null;
        if (model is RegisterViewModel register) register.Password = register.ConfirmPassword = "";
        if (model is LoginViewModel login) login.Password = "";
        if (model is ChangePasswordViewModel change) change.CurrentPassword = change.NewPassword = change.ConfirmPassword = "";
        return View(model);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var field = error.Code switch
            {
                "DuplicateUserName" or "InvalidUserName" => "UserName",
                "DuplicateEmail" or "InvalidEmail" => "Email",
                "PasswordMismatch" => "CurrentPassword",
                "PasswordLength" or "PasswordTooShort" => "Password",
                _ => ""
            };
            ModelState.AddModelError(field, error.Description);
        }
    }
}
