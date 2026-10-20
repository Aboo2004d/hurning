using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class ReportsController : AdminControllerBase
{
    public ReportsController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        ReportManagementService service
    )
        : base(database, urls, settings) => ReportsService = service;

    private ReportManagementService ReportsService
    {
        get;
    }

    public async Task<IActionResult> Reports(int page = 1, CancellationToken ct = default) =>
        View(await ReportsService.Reports().PageAsync(page, ct));

    [HttpPost]
    public async Task<IActionResult> Resolve(long id, CancellationToken ct)
    {
        if (!await ReportsService.ResolveAsync(id, User.Identity?.Name ?? "Admin", ct))
            return NotFound();
        return RedirectToAction(nameof(Reports));
    }
}
