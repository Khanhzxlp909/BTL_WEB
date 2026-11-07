using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Infrastructure;

public static class SafeRedirect
{
    private static readonly Uri LocalBase = new("http://local");

    // Header Location chỉ nhận ASCII: URL nội bộ có ký tự Unicode (vd ?Search=Tuyển) phải được mã hóa
    // trước khi chuyển hướng; URL ngoài hoặc không hợp lệ trả về fallback để chống open redirect.
    public static string LocalOrDefault(IUrlHelper url, string? returnUrl, string fallback = "/")
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !url.IsLocalUrl(returnUrl)) return fallback;
        if (!Uri.TryCreate(LocalBase, returnUrl, out var uri) || uri.Host != LocalBase.Host) return fallback;
        var encoded = uri.PathAndQuery + uri.Fragment;
        return url.IsLocalUrl(encoded) ? encoded : fallback;
    }
}
