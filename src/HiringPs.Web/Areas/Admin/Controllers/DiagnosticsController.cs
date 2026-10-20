using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class DiagnosticsController : AdminControllerBase
{
    private readonly CollectorDiagnosticReadService diagnostics;

    public DiagnosticsController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        CollectorDiagnosticReadService diagnostics
    )
        : base(database, urls, settings) => this.diagnostics = diagnostics;

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Diagnostic(Guid id, int page = 1, CancellationToken ct = default)
    {
        var report = await diagnostics.ReadAsync(id, ct);
        if (report is null)
            return NotFound();
        page = AdminPager.Normalize(page, report.Network.Count);
        ViewData["NetworkPager"] = new AdminPager { Page = page, TotalRecords = report.Network.Count };
        report.Network = report.Network.Skip((page - 1) * AdminPager.PageSize).Take(AdminPager.PageSize).ToList();
        return View(report);
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DiagnosticImage(Guid id, CancellationToken ct)
    {
        var path = await diagnostics.ImagePathAsync(id, ct);
        return path is null ? NotFound() : PhysicalFile(path, "image/jpeg");
    }
}
