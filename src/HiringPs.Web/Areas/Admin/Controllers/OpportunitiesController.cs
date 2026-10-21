using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class OpportunitiesController : AdminControllerBase
{
    private readonly OpportunityManagementService management;
    private readonly AdminOpportunityReadService reader;
    private readonly ICategoryCatalogReadService categories;

    public OpportunitiesController(HiringDb database, UrlPolicy urls, IConfiguration settings,
        OpportunityManagementService? managementService = null, AdminOpportunityReadService? readService = null,
        ICategoryCatalogReadService? categoryCatalog = null)
        : base(database, urls, settings)
    {
        management = managementService ?? new(database);
        reader = readService ?? new(database);
        categories = categoryCatalog ?? new CategoryCatalogService(database);
    }

    public async Task<IActionResult> Opportunities(
        PublicationStatus? status,
        int page = 1,
        CancellationToken ct = default
    )
    {
        status ??= PublicationStatus.Published;
        if (status is not (PublicationStatus.Published or PublicationStatus.NeedsReview or PublicationStatus.Archived or PublicationStatus.Draft)) return BadRequest();
        ViewBag.Status = status;
        ViewBag.Counts = await reader.CountsAsync(ct);
        var model = await reader.Opportunities(status).PageAsync(page, ct);
        return Request.Headers["X-Requested-With"] == "fetch" ? PartialView("_OpportunityRows", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.OriginalSources = Array.Empty<string>();
        return View("OpportunityForm", new OpportunityInput { Status = PublicationStatus.Draft,
            AvailableCategories = await categories.OptionsAsync(ct) });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var opportunity = await management.ReadAsync(id.Value, ct);
        if (opportunity is null)
            return NotFound();
        await SetOriginalSourcesAsync(opportunity.Id, ct);
        var input = OpportunityInput.From(opportunity);
        input.AvailableCategories = await categories.OptionsAsync(ct);
        return View("OpportunityForm", input);
    }

    [HttpPost]
    public Task<IActionResult> Create(
        [Bind(
            "Id,Kind,Title,Organization,OriginalLanguage,Summary,Locations,Category,EmploymentType,ExperienceLevel,WorkMode,ReferenceNumber,Duration,Eligibility,Cost,ApplyUrl,Deadline,Status,ClosureEvidence,Version"
        )]
            OpportunityInput input,
        string? sourceUrl,
        CancellationToken ct
    )
    {
        if (input.Id != 0)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, sourceUrl, ct);
    }

    [HttpPost]
    public Task<IActionResult> Edit(
        [FromRoute] long id,
        [Bind(
            "Id,Kind,Title,Organization,OriginalLanguage,Summary,Locations,Category,EmploymentType,ExperienceLevel,WorkMode,ReferenceNumber,Duration,Eligibility,Cost,ApplyUrl,Deadline,Status,ClosureEvidence,Version"
        )]
            OpportunityInput input,
        string? sourceUrl,
        CancellationToken ct
    )
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, sourceUrl, ct);
    }

    private async Task<IActionResult> SaveAsync(
        OpportunityInput input,
        string? sourceUrl,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        input.AvailableCategories = await categories.OptionsAsync(ct);
        await SetOriginalSourcesAsync(input.Id, ct);
        if (!ModelState.IsValid)
        {
            ViewBag.SourceUrl = sourceUrl;
            return View("OpportunityForm", input);
        }
        var result = await management
            .SaveAsync(input.ToDto(), sourceUrl, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == OpportunitySaveOutcome.NotFound) return NotFound();
        if (result.Outcome != OpportunitySaveOutcome.Saved)
        {
            foreach (var error in result.Errors ?? []) ModelState.AddModelError("", error);
            ViewBag.SourceUrl = sourceUrl;
            return View("OpportunityForm", input);
        }
        return RedirectToAction(nameof(Opportunities));
    }

    private async Task SetOriginalSourcesAsync(long id, CancellationToken ct) =>
        ViewBag.OriginalSources = await db
            .SourceLinks.AsNoTracking()
            .Where(x => x.OpportunityId == id)
            .Select(x => x.SourceItem.Url)
            .Distinct()
            .ToListAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Merge(long id, long targetId, CancellationToken ct)
    {
        try
        {
            if (
                !await MergeHistory.MergeAsync(db, id, targetId, User.Identity?.Name ?? "Admin", ct)
            )
                return BadRequest();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Notice"] = "MergeUndoConflict";
            return RedirectToAction("Merges", "Merges");
        }
        return RedirectToAction(nameof(Edit), new { id = targetId });
    }
}
