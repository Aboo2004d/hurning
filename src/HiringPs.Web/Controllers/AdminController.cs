using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class AdminDashboard
{
    public int Published { get; set; }
    public int Review { get; set; }
    public int Queued { get; set; }
    public int Reports { get; set; }
    public WorkerHeartbeat? Heartbeat { get; set; }
    public List<ScrapeRun> Runs { get; set; } = [];
    public List<SourceItem> Items { get; set; } = [];
}
[Authorize(Roles = "Admin")]
public class AdminController(HiringDb db, UrlPolicy urlPolicy) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(new AdminDashboard { Published = await db.Opportunities.CountAsync(x => x.Status == PublicationStatus.Published, ct), Review = await db.Opportunities.CountAsync(x => x.Status == PublicationStatus.NeedsReview, ct), Queued = await db.SourceItems.CountAsync(x => x.Status == ProcessingStatus.Discovered || x.Status == ProcessingStatus.AwaitingAi || x.Status == ProcessingStatus.Retry, ct), Reports = await db.Reports.CountAsync(x => !x.Resolved, ct), Heartbeat = await db.Heartbeats.AsNoTracking().OrderBy(x=>x.Id).FirstOrDefaultAsync(ct), Runs = await db.Runs.AsNoTracking().OrderByDescending(x => x.Id).Take(20).ToListAsync(ct), Items = await db.SourceItems.AsNoTracking().Include(x => x.Source).Where(x => x.Status != ProcessingStatus.Published).OrderByDescending(x => x.Id).Take(50).ToListAsync(ct) });
    public async Task<IActionResult> Opportunities(PublicationStatus? status, CancellationToken ct) { ViewBag.Status = status; return View(await db.Opportunities.AsNoTracking().Where(x => status == null || x.Status == status).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct)); }
    public async Task<IActionResult> Edit(long? id, CancellationToken ct) { var o = id is null ? new Opportunity { Status = PublicationStatus.Draft } : await db.Opportunities.FindAsync([id.Value], ct); return o is null ? NotFound() : View(o); }
    [HttpPost]
    public async Task<IActionResult> Edit([Bind("Id,Kind,Title,Organization,OriginalLanguage,Summary,Locations,Category,EmploymentType,ExperienceLevel,WorkMode,ReferenceNumber,Duration,Eligibility,Cost,ApplyUrl,Deadline,Status,ClosureEvidence,Version")] Opportunity input, string? sourceUrl, CancellationToken ct)
    {
        NormalizeStrings(input);
        foreach (var error in OpportunityService.Validate(input)) ModelState.AddModelError("", error);
        if (!Enum.IsDefined(input.Status)) ModelState.AddModelError("", "Invalid publication status.");
        if (input.Status == PublicationStatus.Archived && string.IsNullOrWhiteSpace(input.ClosureEvidence) && input.Deadline is null) ModelState.AddModelError("", "Closure evidence is required without a deadline.");
        if (sourceUrl?.Length > 1500) ModelState.AddModelError("", "Source URL is too long.");
        if (!string.IsNullOrWhiteSpace(sourceUrl) && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")) ModelState.AddModelError("", "Source URL must use HTTPS.");
        if (input.Id == 0 && string.IsNullOrWhiteSpace(sourceUrl) && string.IsNullOrWhiteSpace(input.ApplyUrl)) ModelState.AddModelError("", "A source or application link is required.");
        if (!ModelState.IsValid) { ViewBag.SourceUrl = sourceUrl; return View(input); }
        var entity = input.Id == 0 ? new Opportunity() : await db.Opportunities.FindAsync([input.Id], ct);
        if (entity is null) return NotFound();
        if (input.Id != 0) db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        entity.Kind = input.Kind; entity.Title = input.Title.Trim(); entity.Organization = input.Organization.Trim(); entity.OriginalLanguage = input.OriginalLanguage;
        entity.Summary = input.Summary.Trim(); entity.Locations = input.Locations; entity.Category = input.Category; entity.EmploymentType = input.EmploymentType; entity.ExperienceLevel = input.ExperienceLevel; entity.WorkMode = input.WorkMode; entity.ReferenceNumber = input.ReferenceNumber; entity.Duration = input.Duration; entity.Eligibility = input.Eligibility; entity.Cost = input.Cost; entity.ApplyUrl = input.ApplyUrl; entity.Deadline = input.Deadline; entity.Status = input.Status; entity.ClosureEvidence = input.ClosureEvidence; entity.AdminOverride = true; entity.LastVerifiedAt = DateTimeOffset.UtcNow; entity.ReviewReason = "";
        if (entity.Status == PublicationStatus.Published && entity.Deadline is { } deadline && deadline < TextRules.PalestineToday()) { entity.Status = PublicationStatus.Archived; entity.ClosureEvidence = "Published deadline has passed."; }
        entity.Fingerprint = TextRules.Hash(TextRules.Normalize($"{entity.Kind}|{entity.Title}|{entity.Organization}|{entity.Locations}|{entity.ReferenceNumber}|{entity.Deadline}"));
        if (input.Id == 0)
        {
            db.Opportunities.Add(entity);
            var source = await db.Sources.FirstOrDefaultAsync(x => x.Name == "Manual entries", ct);
            if (source is null) { source = new Source { Name = "Manual entries", Url = "https://hiring.ps", Connector = ConnectorKind.BrowserSelectors, Enabled = false }; db.Sources.Add(source); }
            var item = new SourceItem { Source = source, ExternalId = Guid.NewGuid().ToString("N"), Url = sourceUrl ?? input.ApplyUrl, Content = input.Summary, Status = ProcessingStatus.Published, ContentHash = TextRules.Hash(input.Summary) };
            db.SourceItems.Add(item); db.SourceLinks.Add(new() { Opportunity = entity, SourceItem = item, ChildKey = "manual" });
        }
        Audit("Opportunity.Edit", $"Opportunity {input.Id}; status {entity.Status}");
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "The record changed; reload before saving."); return View(input); }
        return RedirectToAction(nameof(Opportunities));
    }
    public async Task<IActionResult> Sources(CancellationToken ct) => View(await db.Sources.AsNoTracking().Include(x=>x.CollectionRequests).OrderBy(x => x.Id).ToListAsync(ct));
    public async Task<IActionResult> Source(int? id, CancellationToken ct) { var source = id is null ? new Source { Connector = ConnectorKind.Automatic } : await db.Sources.FindAsync([id.Value], ct); return source is null ? NotFound() : View(source); }
    [HttpPost]
    public async Task<IActionResult> Source([Bind("Id,Name,Url,Connector,Enabled,UsageApproved,ApprovalNote,ConfigurationJson,IntervalHours,CollectionStartTime,Version")] Source input, CancellationToken ct)
    {
        NormalizeStrings(input);
        var previous=input.Id==0?null:await db.Sources.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==input.Id,ct);
        if(input.Id>0&&previous is null)return NotFound();
        if (!Enum.IsDefined(input.Connector)) ModelState.AddModelError("", "Invalid connector.");
        if (input.IntervalHours is < 1 or > 168) ModelState.AddModelError("", "Interval must be 1–168 hours.");
        if (input.ConfigurationJson.Length > 10000) ModelState.AddModelError("", "Configuration is too large.");
        try
        {
            var conf = JsonSerializer.Deserialize<SourceConfiguration>(input.ConfigurationJson) ?? throw new InvalidOperationException("Invalid configuration.");
            if (conf.MaxPages is < 1 or > 50) throw new InvalidOperationException("MaxPages must be 1–50.");
            if (input.Enabled && (!input.UsageApproved || input.ApprovalNote.Length == 0)) throw new InvalidOperationException("Source usage approval and a record of it are required before enabling.");
            if(!Uri.TryCreate(input.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||!uri.IsDefaultPort)throw new InvalidOperationException("Source URL must use public HTTPS without credentials.");
            if(previous is null||previous.Url!=input.Url||previous.ConfigurationJson!=input.ConfigurationJson||(!previous.Enabled&&input.Enabled))await urlPolicy.ValidateAsync(input.Url,conf.AllowedHosts.Append(uri.IdnHost),ct);
            if (input.Enabled && input.Connector == ConnectorKind.BrowserSelectors && conf.ListingLinkSelector.Length == 0) throw new InvalidOperationException("ListingLinkSelector is required.");
            if (input.Enabled && input.Connector == ConnectorKind.ReliefWeb) throw new InvalidOperationException("ReliefWeb adapter is pending appname approval and verification.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { ModelState.AddModelError("", ex is InvalidOperationException ? ex.Message : "Invalid public source URL/configuration."); }
        if (!ModelState.IsValid) return View(input);
        var entity = input.Id == 0 ? new Source() : await db.Sources.FindAsync([input.Id], ct); if (entity is null) return NotFound();
        if (input.Id != 0) db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        var scheduleChanged = entity.IntervalHours != input.IntervalHours || entity.CollectionStartTime != input.CollectionStartTime || (!entity.Enabled && input.Enabled) || input.Id==0;
        var reconfigured = entity.Url != input.Url || entity.ConfigurationJson != input.ConfigurationJson;
        entity.Name = input.Name; entity.Url = input.Url; entity.Connector = input.Connector; entity.Enabled = input.Enabled; entity.UsageApproved = input.UsageApproved; entity.ApprovalNote = input.ApprovalNote; entity.ConfigurationJson = input.ConfigurationJson; entity.IntervalHours = input.IntervalHours;
        entity.CollectionStartTime=input.CollectionStartTime;
        if(scheduleChanged||reconfigured){entity.ScheduleAnchorDate=SourceSchedule.LocalDate(DateTimeOffset.UtcNow);entity.NextDueAt=SourceSchedule.NextAfter(entity,DateTimeOffset.UtcNow);entity.CollectionRetryAt=null;}
        if (input.Id == 0) db.Sources.Add(entity); Audit("Source.Edit", $"Source {input.Id}; enabled {input.Enabled}");
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Source changed; reload before saving."); return View(input); }
        return RedirectToAction(nameof(Sources));
    }
    [HttpPost]
    public async Task<IActionResult> CollectNow(int id,CancellationToken ct)
    {
        var source=await db.Sources.FindAsync([id],ct);if(source is null)return NotFound();
        if(!source.Enabled||!source.UsageApproved||source.ApprovalNote.Length==0){TempData["Notice"]="CollectNotApproved";return RedirectToAction(nameof(Sources));}
        var requests=db.Set<SourceCollectionRequest>();var request=await requests.FindAsync([id],ct);
        if(request?.Status is "Pending" or "Running"){TempData["Notice"]="CollectAlreadyQueued";return RedirectToAction(nameof(Sources));}
        if(request is null){request=new(){SourceId=id};requests.Add(request);}
        request.RequestToken=Guid.NewGuid();request.RequestedAt=DateTimeOffset.UtcNow;request.StartedAt=null;request.FinishedAt=null;request.DiscoveryFinished=false;request.Status="Pending";request.Error="";
        Audit("Source.CollectNow",$"Source {id}; token {request.RequestToken}");
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){TempData["Notice"]="CollectAlreadyQueued";return RedirectToAction(nameof(Sources));}
        TempData["Notice"]="CollectQueued";return RedirectToAction(nameof(Sources));
    }
    public async Task<IActionResult> Item(long id, CancellationToken ct) { var item = await db.SourceItems.AsNoTracking().Include(x => x.Source).FirstOrDefaultAsync(x => x.Id == id, ct); return item is null ? NotFound() : View(item); }
    [HttpPost] public async Task<IActionResult> Retry(long id, CancellationToken ct) { var item = await db.SourceItems.FindAsync([id], ct); if (item is null) return NotFound(); item.Status = item.Content.Length > 0 ? ProcessingStatus.AwaitingAi : ProcessingStatus.Discovered; item.RetryAt = null; item.Attempts = 0; Audit("Item.Retry", $"Item {id}"); await db.SaveChangesAsync(ct); return RedirectToAction(nameof(Item), new { id }); }
    public async Task<IActionResult> Reports(CancellationToken ct) => View(await db.Reports.AsNoTracking().Include(x => x.Opportunity).OrderBy(x => x.Resolved).ThenByDescending(x => x.Id).Take(100).ToListAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Merge(long id, long targetId, CancellationToken ct)
    {
        if (id == targetId) return BadRequest(); await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var duplicate = await db.Opportunities.FindAsync([id], ct); var target = await db.Opportunities.FindAsync([targetId], ct);
        if (duplicate is null || target is null || duplicate.Kind != target.Kind) return BadRequest();
        foreach (var link in await db.SourceLinks.Where(x => x.OpportunityId == id).ToListAsync(ct)) link.OpportunityId = targetId;
        duplicate.Status = PublicationStatus.Hidden; duplicate.AdminOverride = true; duplicate.ReviewReason = $"Merged by administrator into {targetId}.";
        target.AdminOverride = true; Audit("Opportunity.Merge", $"{id} -> {targetId}"); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return RedirectToAction(nameof(Edit), new { id = targetId });
    }
    [HttpPost] public async Task<IActionResult> Resolve(long id, CancellationToken ct) { var report = await db.Reports.FindAsync([id], ct); if (report is null) return NotFound(); report.Resolved = true; Audit("Report.Resolve", $"Report {id}"); await db.SaveChangesAsync(ct); return RedirectToAction(nameof(Reports)); }
    private void Audit(string action, string detail) => db.Audit.Add(new() { Actor = User.Identity?.Name ?? "Admin", Action = action, Detail = detail });
    private static void NormalizeStrings(object input) { foreach (var property in input.GetType().GetProperties().Where(x => x.PropertyType == typeof(string) && x.CanWrite)) if (property.GetValue(input) is null) property.SetValue(input, ""); }
}
