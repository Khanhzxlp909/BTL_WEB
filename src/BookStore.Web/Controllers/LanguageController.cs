using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public sealed class LanguageController : Controller
{
    [HttpPost("/ngon-ngu")]
    public IActionResult Change(string culture, string? returnUrl)
    {
        if (culture is not ("vi" or "en")) throw new AppException(400, "CULTURE_UNSUPPORTED",
            ValidationText.Pick("Ngôn ngữ không được hỗ trợ. Hãy chọn vi hoặc en.", "The language is unsupported. Choose vi or en."));
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true,
                IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps });
        HttpContext.Session.SetString("Culture", culture);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
