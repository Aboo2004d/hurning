using System.Security.Claims;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace HiringPs.Web.Services.Authentication;

/// <summary>يقرأ حالة ربط جوجل المحلية دون طلب إلى جوجل ودون استعلامات Identity داخل Razor.</summary>
public sealed class GoogleLinkReadService(IAuthenticationSchemeProvider schemes, UserManager<IdentityUser> users)
{
    /// <summary>يفحص تهيئة المخطط وملكية الجلسة ثم يعيد الأزرار المتاحة دون بيانات الحساب الداخلية.</summary>
    public async Task<GoogleLinkStatusDto> ReadAsync(ClaimsPrincipal principal)
    {
        if (await schemes.GetSchemeAsync("Google") is null
            || principal.Identity?.IsAuthenticated != true || principal.IsInRole("Admin"))
            return new(false, false, false);
        var user = await users.GetUserAsync(principal);
        if (user is null)
            return new(false, false, false);
        var linked = (await users.GetLoginsAsync(user)).Any(x => x.LoginProvider == "Google");
        return new(true, linked, linked && await users.HasPasswordAsync(user));
    }
}
