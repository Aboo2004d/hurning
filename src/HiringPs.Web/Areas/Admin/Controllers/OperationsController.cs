using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OperationsController(OperationReadService operations) : Controller
{
    /// <summary>يعرض سجل العمليات بمرشحات SQL وترقيم ثابت دون تغيير أي حالة تشغيلية.</summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] OperationFilterDto filter, int page = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || filter.Area is { } area && !Enum.IsDefined(area)
            || filter.Outcome is { } outcome && !Enum.IsDefined(outcome)
            || filter.From > filter.To)
            return BadRequest();
        return View(new OperationLogPage(filter, await operations.Query(filter).PageAsync(page, ct)));
    }
}
