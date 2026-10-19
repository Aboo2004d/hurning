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
    public OpportunitiesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Opportunities(
        PublicationStatus? status,
        int page = 1,
        CancellationToken ct = default
    )
    {
        status ??= PublicationStatus.Published;
        if (status is not (PublicationStatus.Published or PublicationStatus.NeedsReview or PublicationStatus.Archived or PublicationStatus.Draft)) return BadRequest();
        ViewBag.Status = status;
        ViewBag.Counts = await db.Opportunities.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var model = await db
                .Opportunities.AsNoTracking()
                .Where(x => status == null || x.Status == status)
                .OrderByDescending(x => x.Id)
                .PageAsync(page, ct);
        return Request.Headers["X-Requested-With"] == "fetch" ? PartialView("_OpportunityRows", model) : View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.OriginalSources = Array.Empty<string>();
        return View("OpportunityForm", new Opportunity { Status = PublicationStatus.Draft });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var opportunity = await db.Opportunities.FindAsync([id.Value], ct);
        if (opportunity is null)
            return NotFound();
        await SetOriginalSourcesAsync(opportunity.Id, ct);
        return View("OpportunityForm", opportunity);
    }

    [HttpPost]
    public Task<IActionResult> Create(
        [Bind(
            "Id,Kind,Title,Organization,OriginalLanguage,Summary,Locations,Category,EmploymentType,ExperienceLevel,WorkMode,ReferenceNumber,Duration,Eligibility,Cost,ApplyUrl,Deadline,Status,ClosureEvidence,Version"
        )]
            Opportunity input,
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
            Opportunity input,
        string? sourceUrl,
        CancellationToken ct
    )
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, sourceUrl, ct);
    }

    private async Task<IActionResult> SaveAsync(
        Opportunity input,
        string? sourceUrl,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        await SetOriginalSourcesAsync(input.Id, ct);
        foreach (var error in OpportunityService.Validate(input, allowUnrecognizedLocation: true))
            ModelState.AddModelError("", error);
        if (!Enum.IsDefined(input.Status))
            ModelState.AddModelError("", "Invalid publication status.");
        if (
            input.Status == PublicationStatus.Archived
            && string.IsNullOrWhiteSpace(input.ClosureEvidence)
            && input.Deadline is null
        )
            ModelState.AddModelError("", "Closure evidence is required without a deadline.");
        if (sourceUrl?.Length > 1500)
            ModelState.AddModelError("", "Source URL is too long.");
        if (
            !string.IsNullOrWhiteSpace(sourceUrl)
            && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        )
            ModelState.AddModelError("", "Source URL must use HTTPS.");
        if (
            input.Id == 0
            && string.IsNullOrWhiteSpace(sourceUrl)
            && string.IsNullOrWhiteSpace(input.ApplyUrl)
        )
            ModelState.AddModelError("", "A source or application link is required.");
        if (!ModelState.IsValid)
        {
            ViewBag.SourceUrl = sourceUrl;
            return View("OpportunityForm", input);
        }

        var entity =
            input.Id == 0 ? new Opportunity() : await db.Opportunities.FindAsync([input.Id], ct);
        if (entity is null)
            return NotFound();
        if (input.Id != 0)
            db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        entity.Kind = input.Kind;
        entity.Title = input.Title.Trim();
        entity.Organization = input.Organization.Trim();
        entity.OriginalLanguage = input.OriginalLanguage;
        entity.Summary = input.Summary.Trim();
        entity.Locations = input.Locations;
        entity.Category = input.Category;
        entity.EmploymentType = input.EmploymentType;
        entity.ExperienceLevel = input.ExperienceLevel;
        entity.WorkMode = input.WorkMode;
        entity.ReferenceNumber = input.ReferenceNumber;
        entity.Duration = input.Duration;
        entity.Eligibility = input.Eligibility;
        entity.Cost = input.Cost;
        entity.ApplyUrl = input.ApplyUrl;
        entity.Deadline = input.Deadline;
        entity.Status = input.Status;
        entity.ClosureEvidence = input.ClosureEvidence;
        entity.AdminOverride = true;
        entity.LastVerifiedAt = DateTimeOffset.UtcNow;
        entity.ReviewReason = "";
        if (
            entity.Status == PublicationStatus.Published
            && entity.Deadline is { } deadline
            && deadline < TextRules.PalestineToday()
        )
        {
            entity.Status = PublicationStatus.Archived;
            entity.ClosureEvidence = "Published deadline has passed.";
        }

        entity.Fingerprint = TextRules.Hash(
            TextRules.Normalize(
                $"{entity.Kind}|{entity.Title}|{entity.Organization}|{entity.Locations}|{entity.ReferenceNumber}|{entity.Deadline}"
            )
        );
        if (input.Id == 0)
        {
            db.Opportunities.Add(entity);
            var source = await db.Sources.FirstOrDefaultAsync(x => x.Name == "Manual entries", ct);
            if (source is null)
            {
                source = new Source
                {
                    Name = "Manual entries",
                    Url = "https://hiring.ps",
                    Connector = ConnectorKind.BrowserSelectors,
                    Enabled = false,
                };
                db.Sources.Add(source);
            }

            var item = new SourceItem
            {
                Source = source,
                ExternalId = Guid.NewGuid().ToString("N"),
                Url = sourceUrl ?? input.ApplyUrl,
                Content = input.Summary,
                Status = ProcessingStatus.Published,
                ContentHash = TextRules.Hash(input.Summary),
            };
            db.SourceItems.Add(item);
            db.SourceLinks.Add(
                new()
                {
                    Opportunity = entity,
                    SourceItem = item,
                    ChildKey = "manual",
                }
            );
        }

        Audit("Opportunity.Edit", $"Opportunity {input.Id}; status {entity.Status}");
        if (entity.Status is PublicationStatus.Published or PublicationStatus.Archived)
        {
            var known = await LocationCatalog.ApprovedAsync(db, ct);
            foreach (
                var location in LocationCatalog
                    .Split(entity.Locations)
                    .Where(x => !known.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            )
                Audit("Location.Approved", location);
        }

        if (input.Id > 0)
        {
            var rawIds = await db
                .SourceLinks.Where(x => x.OpportunityId == input.Id)
                .Select(x => x.SourceItemId)
                .Distinct()
                .ToListAsync(ct);
            foreach (var rawId in rawIds)
            {
                var raw = await db.SourceItems.FindAsync([rawId], ct);
                if (raw is null || raw.Status != ProcessingStatus.NeedsReview)
                    continue;
                var links = await db
                    .SourceLinks.Include(x => x.Opportunity)
                    .Where(x => x.SourceItemId == rawId)
                    .ToListAsync(ct);
                var pending = links
                    .Where(x => x.Opportunity.Status == PublicationStatus.NeedsReview)
                    .ToList();
                raw.Status =
                    pending.Count == 0 ? ProcessingStatus.Published : ProcessingStatus.NeedsReview;
                raw.Error = string.Join(
                        " ",
                        pending.Select(x => x.Opportunity.ReviewReason).Distinct()
                    )
                    .Truncate(1800);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "The record changed; reload before saving.");
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
