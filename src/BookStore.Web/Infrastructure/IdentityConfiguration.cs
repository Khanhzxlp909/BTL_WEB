using BookStore.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookStore.Web.Infrastructure;

public sealed class BookStorePasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        => Task.FromResult(password is { Length: >= 10 and <= 64 } ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = "PasswordLength", Description = ValidationText.PasswordLength }));
}

public sealed class BookStoreIdentityErrors : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = ValidationText.Pick("Tên đăng nhập đã được sử dụng. Hãy chọn tên khác.", "That username is already in use. Choose another username.") };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = ValidationText.Pick("Email đã được sử dụng. Hãy chọn email khác hoặc đăng nhập.", "That email is already in use. Use another email or sign in.") };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = ValidationText.UserName };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = ValidationText.Email };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = ValidationText.Pick("Mật khẩu hiện tại không đúng. Hãy nhập lại; mật khẩu cũ chưa bị thay đổi.", "The current password is incorrect. Try again; your password has not changed.") };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = ValidationText.Pick("Tài khoản vừa được cập nhật từ phiên khác. Tải lại trang rồi thực hiện lại.", "Your account was updated by another session. Reload the page and try again.") };
    public override IdentityError DefaultError() => new() { Code = "IDENTITY_OPERATION_REJECTED", Description = ValidationText.Pick("Không thể cập nhật tài khoản vì dữ liệu xác thực không đáp ứng điều kiện của Identity. Kiểm tra dữ liệu hoặc liên hệ quản trị viên.", "Identity rejected the account update because its authentication requirements were not met. Check your input or contact an administrator.") };
}

public sealed class ActiveUserSignInManager(UserManager<ApplicationUser> users, IHttpContextAccessor accessor,
    IUserClaimsPrincipalFactory<ApplicationUser> factory, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(users, accessor, factory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
        => user.IsActive && await base.CanSignInAsync(user);

    public override async Task<ApplicationUser?> ValidateSecurityStampAsync(System.Security.Claims.ClaimsPrincipal? principal)
    {
        var user = await base.ValidateSecurityStampAsync(principal);
        return user is { IsActive: true } ? user : null;
    }
}
