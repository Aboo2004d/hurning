using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class MergesController : AdminControllerBase
{
    public MergesController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        MergeAdministrationService service
    )
        : base(database, urls, settings) => MergeService = service;

    public async Task<IActionResult> Merges(long? opportunityId, int page = 1, CancellationToken ct = default) =>
        View(await MergeService.History(opportunityId).PageAsync(page, ct));

    private MergeAdministrationService MergeService
    {
        get;
    }

    [HttpPost]
    public async Task<IActionResult> UndoMerge(long id, CancellationToken ct)
    {
        TempData["Notice"] = await MergeService.UndoAsync(id, User.Identity?.Name ?? "Admin", ct) ? "MergeUndone" : "MergeUndoConflict";
        return RedirectToAction(nameof(Merges));
    }
}
