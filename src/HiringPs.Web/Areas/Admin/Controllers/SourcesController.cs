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

public class SourcesController : AdminControllerBase
{
    public SourcesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    private static readonly SemaphoreSlim SourceSetupGate = new(1, 1);

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
        var duplicateSource = await ExistingSourceAsync(input.Url, input.Id, ct);
        if (duplicateSource is not null)
        {
            ViewData["DuplicateSourceName"] = duplicateSource.Name;
            return View(input);
        }
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
            entity.Url != input.Url
            || entity.ConfigurationJson != input.ConfigurationJson
            || entity.Connector != input.Connector;
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

    [HttpPost]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> SuggestSource(
        string? url,
        int sourceId,
        [FromServices] SourceSetupAssistant assistant,
        CancellationToken ct
    )
    {
        if (!await SourceSetupGate.WaitAsync(0, ct))
            return StatusCode(429, new { error = "SourceSetupBusy" });
        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromMinutes(3));
            var existing = await ExistingSourceAsync(url ?? "", sourceId, budget.Token);
            if (existing is not null)
                return Conflict(new { error = "SourceSetupDuplicate", sourceName = existing.Name });
            var result = await assistant.SuggestAsync(
                url?.Trim() ?? "",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                budget.Token
            );
            existing = await ExistingSourceAsync(result.Url, sourceId, budget.Token);
            if (existing is not null)
                return Conflict(new { error = "SourceSetupDuplicate", sourceName = existing.Name });
            Audit("Source.SetupPreview", new Uri(result.Url).IdnHost);
            await db.SaveChangesAsync(budget.Token);
            return Json(result);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return StatusCode(
                408,
                new
                {
                    error = "SourceSetupTimeout",
                    detail = SourceSetupErrorDetail(ex),
                    stage = ex.Data["SourceSetupStage"] as string,
                    reference = HttpContext.TraceIdentifier,
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext
                .RequestServices.GetRequiredService<ILogger<SourceSetupAssistant>>()
                .LogWarning(
                    ex,
                    "Source setup failed for {Host}; stage {Stage}; request {RequestId}.",
                    Uri.TryCreate(url, UriKind.Absolute, out var target)
                        ? target.IdnHost
                        : "invalid",
                    ex.Data["SourceSetupStage"] as string ?? "SourceSetupStageConfiguration",
                    HttpContext.TraceIdentifier
                );
            var codes = new[]
            {
                "SourceSetupInvalidUrl",
                "SourceSetupSocialDeferred",
                "SourceSetupUnsupported",
                "SourceSetupUnseenUrl",
                "SourceSetupInvalidReply",
                "SourceSetupNoLinks",
                "SourceSetupPageTooLarge",
                "SourceSetupInteractivePagination",
                "SourceSetupPaginationMissing",
                "SourceSetupAiMissing",
                "SourceSetupAiRejected",
            };
            var original = ex is CollectorDiagnosticException diagnostic
                ? diagnostic.InnerException ?? ex
                : ex;
            var error =
                codes.Contains(ex.Message) ? ex.Message
                : original is TimeoutException ? "SourceSetupTimeout"
                : original is System.Net.Http.HttpRequestException ? "SourceSetupNetworkFailed"
                : original is System.Text.Json.JsonException ? "SourceSetupInvalidReply"
                : original is ContentNeedsReviewException ? "SourceSetupSampleInvalid"
                : original is Microsoft.Playwright.PlaywrightException ? "SourceSetupBrowserFailed"
                : "SourceSetupFailed";
            return BadRequest(
                new
                {
                    error,
                    detail = SourceSetupErrorDetail(ex),
                    stage = ex.Data["SourceSetupStage"] as string,
                    reference = HttpContext.TraceIdentifier,
                }
            );
        }
        finally
        {
            SourceSetupGate.Release();
        }
    }

    private string SourceSetupErrorDetail(Exception exception)
    {
        var messages = new List<string>();
        for (
            Exception? current = exception;
            current is not null && messages.Count < 8;
            current = current.InnerException
        )
            messages.Add($"{current.GetType().Name}: {current.Message}");
        var detail = string.Join("\n", messages);
        foreach (
            var setting in configuration
                .AsEnumerable()
                .Where(s =>
                    s.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(
                        s.Key,
                        @"(?:^|:)(?:ApiKey|SecretKey|Secret|Password|Token|ClientSecret)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    )
                )
        )
            if (!string.IsNullOrEmpty(setting.Value) && setting.Value.Length >= 4)
                detail = detail.Replace(setting.Value, "[REDACTED]", StringComparison.Ordinal);
        detail = System.Text.RegularExpressions.Regex.Replace(
            detail,
            @"(?i)(\b(?:api[_-]?key|access_token|token|password|pwd|client_secret)\s*[=:]\s*)[^\s&;""<>]+",
            "$1[REDACTED]"
        );
        detail = System.Text.RegularExpressions.Regex.Replace(
            detail,
            @"(https?://)[^/\s:@]+:[^/\s@]+@",
            "$1[REDACTED]@"
        );
        return detail.Length <= 12000 ? detail : detail[..12000];
    }

    private async Task<Source?> ExistingSourceAsync(
        string url,
        int excludedId,
        CancellationToken ct
    )
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var requested))
            return null;
        var sources = await db
            .Sources.AsNoTracking()
            .Where(s => s.Id != excludedId)
            .ToListAsync(ct);
        return sources.FirstOrDefault(s =>
            Uri.TryCreate(s.Url, UriKind.Absolute, out var saved)
            && Uri.Compare(
                requested,
                saved,
                UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
                UriFormat.UriEscaped,
                StringComparison.Ordinal
            ) == 0
        );
    }
}
