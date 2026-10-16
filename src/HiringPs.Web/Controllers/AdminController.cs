// إجراءات لوحة الإدارة: مراجعة الفرص وإدارة المصادر والرسائل ومحتوى الصفحات.
using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Controllers;

public class AdminDashboard
{
    public int Published { get; set; }
    public int Review { get; set; }
    public int Queued { get; set; }
    public int Reports { get; set; }
    public WorkerHeartbeat? Heartbeat { get; set; }
    public List<ScrapeRun> Runs { get; set; } = [];
    public AdminPager RunsPager { get; set; } = new();
    public List<SourceItem> Items { get; set; } = [];
    public AdminPager ItemsPager { get; set; } = new();
}

[Authorize(Roles = "Admin")]
public partial class AdminController(HiringDb db, UrlPolicy urlPolicy, IConfiguration configuration)
    : Controller
{
    public async Task<IActionResult> Pages(CancellationToken ct) =>
        View(await db.SitePages.AsNoTracking().ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> PageContent(string key, CancellationToken ct)
    {
        if (!HiringPs.Web.Models.SitePageDefaults.IsEditable(key))
            return NotFound();
        var page = await db.SitePages.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        var ar = HiringPs.Web.Models.SitePageDefaults.Resolve(key, page, "ar");
        var en = HiringPs.Web.Models.SitePageDefaults.Resolve(key, page, "en");
        return View(
            new HiringPs.Web.Models.SitePageInput
            {
                Key = key,
                TitleAr = ar.Title,
                BodyAr = ar.Body,
                TitleEn = en.Title,
                BodyEn = en.Body,
                Version = page?.Version ?? [],
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> PageContent(
        HiringPs.Web.Models.SitePageInput input,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        if (!HiringPs.Web.Models.SitePageDefaults.IsEditable(input.Key))
            return NotFound();
        if (!ModelState.IsValid)
            return View(input);
        var page = await db.SitePages.SingleOrDefaultAsync(x => x.Key == input.Key, ct);
        if (page is not null && (input.Version is null || input.Version.Length == 0))
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }

        if (page is null)
        {
            if (input.Version is { Length: > 0 })
            {
                ModelState.AddModelError("", "PageEditConflict");
                return View(input);
            }

            page = new SitePage { Key = input.Key };
            db.SitePages.Add(page);
        }
        else
            db.Entry(page).Property(x => x.Version).OriginalValue = input.Version!;
        page.TitleAr = input.TitleAr.Trim();
        page.TitleEn = input.TitleEn.Trim();
        page.BodyAr = input.BodyAr.Trim();
        page.BodyEn = input.BodyEn.Trim();
        page.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("SitePage.Update", $"Page {page.Key}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
                && sql.Number is 2601 or 2627
            )
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }

        TempData["Notice"] = "PageContentSaved";
        return RedirectToAction(nameof(PageContent), new { key = input.Key });
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Diagnostic(
        Guid id,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var audit = await db
            .Audit.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")),
                ct
            );
        if (audit is null)
            return NotFound();
        var file = CollectorDiagnosticFiles.FilePath(configuration, id);
        if (System.IO.File.Exists(file))
        {
            var report = JsonSerializer.Deserialize<CollectorDiagnostic>(
                await System.IO.File.ReadAllTextAsync(file, ct),
                CollectorDiagnosticFiles.Json
            );
            if (report is null || report.Id != id)
                return NotFound();
            page = AdminPager.Normalize(page, report.Network.Count);
            ViewData["NetworkPager"] = new AdminPager
            {
                Page = page,
                TotalRecords = report.Network.Count,
            };
            report.Network = report
                .Network.Skip((page - 1) * AdminPager.PageSize)
                .Take(AdminPager.PageSize)
                .ToList();
            return View(report);
        }

        using var metadata = JsonDocument.Parse(audit.Detail);
        var root = metadata.RootElement;
        return View(
            new CollectorDiagnostic
            {
                Id = id,
                At = root.GetProperty("At").GetDateTimeOffset(),
                SourceId = root.GetProperty("SourceId").GetInt32(),
                Stage = root.GetProperty("Stage").GetString() ?? "",
                Operation = root.GetProperty("Operation").GetString() ?? "",
                HttpStatus =
                    root.GetProperty("HttpStatus").ValueKind == JsonValueKind.Number
                        ? root.GetProperty("HttpStatus").GetInt32()
                        : null,
                FinalUrl = root.GetProperty("finalUrl").GetString() ?? "",
                Selector = root.GetProperty("selector").GetString() ?? "",
                TimeoutMs = root.GetProperty("TimeoutMs").GetInt32(),
                ElapsedMs = root.GetProperty("ElapsedMs").GetInt64(),
                StageElapsedMs = root.GetProperty("StageElapsedMs").GetInt64(),
                ExceptionType = root.GetProperty("ExceptionType").GetString() ?? "",
                CaptureNote = "Capture storage unavailable; metadata only.",
            }
        );
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DiagnosticImage(Guid id, CancellationToken ct)
    {
        if (
            !await db.Audit.AnyAsync(
                x => x.Action == "Collector.Diagnostic" && x.Detail.Contains(id.ToString("N")),
                ct
            )
        )
            return NotFound();
        var path = CollectorDiagnosticFiles.FilePath(configuration, id, true);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/jpeg") : NotFound();
    }

    public async Task<IActionResult> Index(
        int runsPage = 1,
        int itemsPage = 1,
        CancellationToken ct = default
    )
    {
        var runs = await db
            .Runs.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .PageAsync(runsPage, ct, "runsPage", "#runs");
        var items = await db
            .SourceItems.AsNoTracking()
            .Include(x => x.Source)
            .Where(x => x.Status != ProcessingStatus.Published)
            .OrderByDescending(x => x.Id)
            .PageAsync(itemsPage, ct, "itemsPage", "#queue");
        return View(
            new AdminDashboard
            {
                Published = await db.Opportunities.CountAsync(
                    x => x.Status == PublicationStatus.Published,
                    ct
                ),
                Review = await db.Opportunities.CountAsync(
                    x => x.Status == PublicationStatus.NeedsReview,
                    ct
                ),
                Queued = await db.SourceItems.CountAsync(
                    x =>
                        x.Status == ProcessingStatus.Discovered
                        || x.Status == ProcessingStatus.AwaitingAi
                        || x.Status == ProcessingStatus.Retry,
                    ct
                ),
                Reports = await db.Reports.CountAsync(x => !x.Resolved, ct),
                Heartbeat = await db
                    .Heartbeats.AsNoTracking()
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct),
                Runs = runs.Items,
                RunsPager = runs.Pager,
                Items = items.Items,
                ItemsPager = items.Pager,
            }
        );
    }

    public async Task<IActionResult> Opportunities(
        PublicationStatus? status,
        int page = 1,
        CancellationToken ct = default
    )
    {
        ViewBag.Status = status;
        return View(
            await db
                .Opportunities.AsNoTracking()
                .Where(x => status == null || x.Status == status)
                .OrderByDescending(x => x.Id)
                .PageAsync(page, ct)
        );
    }

    public async Task<IActionResult> Edit(long? id, CancellationToken ct)
    {
        var o = id is null
            ? new Opportunity { Status = PublicationStatus.Draft }
            : await db.Opportunities.FindAsync([id.Value], ct);
        if (o is null)
            return NotFound();
        await SetOriginalSourcesAsync(o.Id, ct);
        return View(o);
    }

    private async Task SetOriginalSourcesAsync(long id, CancellationToken ct) =>
        ViewBag.OriginalSources = await db
            .SourceLinks.AsNoTracking()
            .Where(x => x.OpportunityId == id)
            .Select(x => x.SourceItem.Url)
            .Distinct()
            .ToListAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Edit(
        [Bind(
            "Id,Kind,Title,Organization,OriginalLanguage,Summary,Locations,Category,EmploymentType,ExperienceLevel,WorkMode,ReferenceNumber,Duration,Eligibility,Cost,ApplyUrl,Deadline,Status,ClosureEvidence,Version"
        )]
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
            return View(input);
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
            return View(input);
        }

        return RedirectToAction(nameof(Opportunities));
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Messages(
        string status = "all",
        int page = 1,
        CancellationToken ct = default
    )
    {
        if (status is not ("all" or "unread" or "read" or "handled"))
            status = "all";
        var query = db.ContactMessages.AsNoTracking();
        query = status switch
        {
            "unread" => query.Where(x => !x.IsRead && !x.IsHandled),
            "read" => query.Where(x => x.IsRead && !x.IsHandled),
            "handled" => query.Where(x => x.IsHandled),
            _ => query,
        };
        var count = await query.CountAsync(ct);
        page = AdminPager.Normalize(page, count);
        return View(
            new HiringPs.Web.Models.ContactInbox
            {
                Items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * AdminPager.PageSize)
                    .Take(AdminPager.PageSize)
                    .ToListAsync(ct),
                Status = status,
                Count = count,
                Pager = new() { Page = page, TotalRecords = count },
                UnreadCount = await db.ContactMessages.CountAsync(
                    x => !x.IsRead && !x.IsHandled,
                    ct
                ),
            }
        );
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Message(long id, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return NotFound();
        if (!message.IsRead)
        {
            message.IsRead = true;
            await db.SaveChangesAsync(ct);
        }

        return View(message);
    }

    [HttpPost]
    public async Task<IActionResult> MessageStatus(long id, bool handled, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return NotFound();
        message.IsHandled = handled;
        message.IsRead = true;
        Audit("Contact.Status", $"Message {id}; handled {handled}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Notice"] = "ContactChanged";
        }

        return RedirectToAction(nameof(Message), new { id });
    }

    public async Task<IActionResult> Sources(int page = 1, CancellationToken ct = default)
    {
        var sources = await db
            .Sources.AsNoTracking()
            .Include(x => x.CollectionRequests)
            .OrderBy(x => x.Id)
            .PageAsync(page, ct);
        var ids = sources.Items.Select(x => x.Id).ToArray();
        ViewBag.DiscoveryProgress = await db
            .DiscoveryCheckpoints.AsNoTracking()
            .Where(x => ids.Contains(x.SourceId))
            .ToDictionaryAsync(x => x.SourceId, ct);
        return View(sources);
    }

    public async Task<IActionResult> Source(int? id, CancellationToken ct)
    {
        var source = id is null
            ? new Source { Connector = ConnectorKind.Automatic }
            : await db.Sources.FindAsync([id.Value], ct);
        return source is null ? NotFound() : View(source);
    }

    [HttpPost]
    public async Task<IActionResult> Source(
        [Bind(
            "Id,Name,Url,Connector,Enabled,UsageApproved,ApprovalNote,ConfigurationJson,IntervalHours,CollectionStartTime,Version"
        )]
            Source input,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        var previous =
            input.Id == 0
                ? null
                : await db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.Id, ct);
        if (input.Id > 0 && previous is null)
            return NotFound();
        if (!Enum.IsDefined(input.Connector))
            ModelState.AddModelError("", "Invalid connector.");
        if (input.IntervalHours is < 1 or > 168)
            ModelState.AddModelError("", "Interval must be 1–168 hours.");
        if (input.ConfigurationJson.Length > 10000)
            ModelState.AddModelError("", "Configuration is too large.");
        try
        {
            var conf =
                JsonSerializer.Deserialize<SourceConfiguration>(input.ConfigurationJson)
                ?? throw new InvalidOperationException("Invalid configuration.");
            if (conf.MaxPages is < 1 or > 50)
                throw new InvalidOperationException("MaxPages must be 1–50.");
            if (input.Enabled && (!input.UsageApproved || input.ApprovalNote.Length == 0))
                throw new InvalidOperationException(
                    "Source usage approval and a record of it are required before enabling."
                );
            if (
                !Uri.TryCreate(input.Url, UriKind.Absolute, out var uri)
                || uri.Scheme != "https"
                || uri.UserInfo.Length > 0
                || !uri.IsDefaultPort
            )
                throw new InvalidOperationException(
                    "Source URL must use public HTTPS without credentials."
                );
            if (
                previous is null
                || previous.Url != input.Url
                || previous.ConfigurationJson != input.ConfigurationJson
                || (!previous.Enabled && input.Enabled)
            )
                await urlPolicy.ValidateAsync(input.Url, conf.AllowedHosts.Append(uri.IdnHost), ct);
            if (
                input.Enabled
                && input.Connector == ConnectorKind.BrowserSelectors
                && conf.ListingLinkSelector.Length == 0
            )
                throw new InvalidOperationException("ListingLinkSelector is required.");
            if (
                input.Connector == ConnectorKind.Automatic
                && (
                    input.Url == OpportunityRss.JobsUrl
                    || input.Url == OpportunityRss.ReliefUrl
                    || input.Url == UnCareersFeed.Url
                )
            )
                throw new InvalidOperationException(
                    "This is an RSS URL; choose its feed connector instead of Automatic HTML discovery."
                );
            if (input.Connector == ConnectorKind.UnrwaInspira && input.Url != UnCareersFeed.Url)
                throw new InvalidOperationException(
                    "UNRWA/Inspira connector requires https://careers.un.org/jobfeed."
                );
            if (input.Enabled && input.Connector == ConnectorKind.ReliefWeb)
                throw new InvalidOperationException(
                    "ReliefWeb adapter is pending appname approval and verification."
                );
            if (
                input.Connector is ConnectorKind.JobsRss or ConnectorKind.ReliefWebRss
                && input.Url != OpportunityRss.Endpoint(input.Connector)
            )
                throw new InvalidOperationException(
                    "RSS connector requires its official tested feed URL."
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ModelState.AddModelError(
                "",
                ex is InvalidOperationException
                    ? ex.Message
                    : "Invalid public source URL/configuration."
            );
        }

        if (!ModelState.IsValid)
            return View(input);
        var entity = input.Id == 0 ? new Source() : await db.Sources.FindAsync([input.Id], ct);
        if (entity is null)
            return NotFound();
        if (input.Id != 0)
            db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        var scheduleChanged =
            entity.IntervalHours != input.IntervalHours
            || entity.CollectionStartTime != input.CollectionStartTime
            || (!entity.Enabled && input.Enabled)
            || input.Id == 0;
        var reconfigured =
            entity.Url != input.Url || entity.ConfigurationJson != input.ConfigurationJson;
        entity.Name = input.Name;
        entity.Url = input.Url;
        entity.Connector = input.Connector;
        entity.Enabled = input.Enabled;
        entity.UsageApproved = input.UsageApproved;
        entity.ApprovalNote = input.ApprovalNote;
        entity.ConfigurationJson = input.ConfigurationJson;
        entity.IntervalHours = input.IntervalHours;
        entity.CollectionStartTime = input.CollectionStartTime;
        if (scheduleChanged || reconfigured)
        {
            entity.ScheduleAnchorDate = SourceSchedule.LocalDate(DateTimeOffset.UtcNow);
            entity.NextDueAt = SourceSchedule.NextAfter(entity, DateTimeOffset.UtcNow);
            entity.CollectionRetryAt = null;
        }

        if (input.Id == 0)
            db.Sources.Add(entity);
        Audit("Source.Edit", $"Source {input.Id}; enabled {input.Enabled}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "Source changed; reload before saving.");
            return View(input);
        }

        return RedirectToAction(nameof(Sources));
    }

    [HttpPost]
    public async Task<IActionResult> CollectNow(int id, CancellationToken ct)
    {
        var source = await db.Sources.FindAsync([id], ct);
        if (source is null)
            return NotFound();
        if (!source.Enabled || !source.UsageApproved || source.ApprovalNote.Length == 0)
        {
            TempData["Notice"] = "CollectNotApproved";
            return RedirectToAction(nameof(Sources));
        }

        var requests = db.Set<SourceCollectionRequest>();
        var request = await requests.FindAsync([id], ct);
        if (request?.Status is "Pending" or "Running")
        {
            TempData["Notice"] = "CollectAlreadyQueued";
            return RedirectToAction(nameof(Sources));
        }

        if (request is null)
        {
            request = new() { SourceId = id };
            requests.Add(request);
        }

        request.RequestToken = Guid.NewGuid();
        request.RequestedAt = DateTimeOffset.UtcNow;
        request.StartedAt = null;
        request.FinishedAt = null;
        request.DiscoveryFinished = false;
        request.Status = "Pending";
        request.Error = "";
        Audit("Source.CollectNow", $"Source {id}; token {request.RequestToken}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            TempData["Notice"] = "CollectAlreadyQueued";
            return RedirectToAction(nameof(Sources));
        }

        TempData["Notice"] = "CollectQueued";
        return RedirectToAction(nameof(Sources));
    }

    public async Task<IActionResult> Item(long id, CancellationToken ct)
    {
        var item = await db
            .SourceItems.AsNoTracking()
            .Include(x => x.Source)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        var item = await db
            .SourceItems.Include(x => x.Source)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return NotFound();
        // PDF retries fetch the original again instead of reusing a damaged historical transcription.
        var refreshPdf = PdfRetry.NeedsFreshContent(item);
        item.Status =
            !refreshPdf && item.Content.Length > 0
                ? ProcessingStatus.AwaitingAi
                : ProcessingStatus.Discovered;
        if (refreshPdf)
            item.ExtractionJson = "";
        // Reset retry state
        item.RetryAt = null;
        item.Attempts = 0;
        // Clear previous processing error
        item.Error = string.Empty;
        Audit("Item.Retry", $"Item {id}");
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Item), new { id });
    }

    public async Task<IActionResult> Reports(int page = 1, CancellationToken ct = default) =>
        View(
            await db
                .Reports.AsNoTracking()
                .Include(x => x.Opportunity)
                .OrderBy(x => x.Resolved)
                .ThenByDescending(x => x.Id)
                .PageAsync(page, ct)
        );

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
            return RedirectToAction(nameof(Merges));
        }
        return RedirectToAction(nameof(Edit), new { id = targetId });
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(long id, CancellationToken ct)
    {
        var report = await db.Reports.FindAsync([id], ct);
        if (report is null)
            return NotFound();
        report.Resolved = true;
        Audit("Report.Resolve", $"Report {id}");
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Reports));
    }

    private void Audit(string action, string detail) =>
        db.Audit.Add(
            new()
            {
                Actor = User.Identity?.Name ?? "Admin",
                Action = action,
                Detail = detail,
            }
        );

    private static void NormalizeStrings(object input)
    {
        foreach (
            var property in input
                .GetType()
                .GetProperties()
                .Where(x => x.PropertyType == typeof(string) && x.CanWrite)
        )
            if (property.GetValue(input) is null)
                property.SetValue(input, "");
    }
}
