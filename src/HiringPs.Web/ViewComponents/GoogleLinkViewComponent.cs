using HiringPs.Web.Services.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.ViewComponents;

/// <summary>يعرض ربط جوجل من نموذج حالة صريح بدل تنفيذ استعلامات الهوية في القالب.</summary>
public sealed class GoogleLinkViewComponent(GoogleLinkReadService links) : ViewComponent
{
    /// <summary>يحضر حالة الزر للحساب الحالي ثم يفوض القالب لعرض النتيجة.</summary>
    public async Task<IViewComponentResult> InvokeAsync() => View(await links.ReadAsync(HttpContext.User));
}
