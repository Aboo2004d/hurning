using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SystemErrorsController : Controller
{
    /// <summary>يربط زر أخطاء النظام بواجهة ELMAH الأصلية دون واجهة أخطاء مخصصة.</summary>
    [HttpGet]
    public IActionResult Index() => LocalRedirect((HttpContext.Items["MountPath"]?.ToString() ?? "")
        + ElmahLoggingRegistration.Path + "/");
}
