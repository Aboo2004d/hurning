using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SystemErrorsController(SystemErrorReadService errors) : Controller
{
    /// <summary>يعرض الأخطاء للمسؤول فقط مع بحث ومرشحات وترقيم ثابت للنتائج.</summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SystemErrorFilterDto filter, int page = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || filter.From > filter.To
            || filter.Application is not (null or "" or "Web" or "Worker")
            || filter.Level is not (null or "" or "Warning" or "Error" or "Critical"))
            return BadRequest();
        return View(new SystemErrorsPage(filter, await errors.Query(filter).PageAsync(page, ct)));
    }

    /// <summary>يعرض الاستثناء وموقعه في الكود لسجل محدد دون تغيير أي بيانات.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var entry = await errors.FindAsync(id, ct);
        return entry is null ? NotFound() : View(entry);
    }
}
