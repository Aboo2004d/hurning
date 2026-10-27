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
    private readonly SourceManagementService management;
    private readonly SourceReadService reader;
    private readonly SourceCollectionRequestService collectionRequests;

    public SourcesController(HiringDb database, UrlPolicy urls, IConfiguration settings,
        SourceManagementService? managementService = null, SourceReadService? readService = null,
        SourceCollectionRequestService? requestService = null)
        : base(database, urls, settings)
    {
        management = managementService ?? new(database, urls);
        reader = readService ?? new(database);
        collectionRequests = requestService ?? new(database);
    }

    private static readonly SemaphoreSlim SourceSetupGate = new(1, 1);

    public async Task<IActionResult> Sources(int page = 1, CancellationToken ct = default)
    {
        var sources = await reader.Sources().PageAsync(page, ct);
        ViewBag.DiscoveryProgress = await reader.ProgressAsync(sources.Items.Select(x => x.Id).ToArray(), ct);
        return View(sources);
    }

    [HttpGet]
    public IActionResult Create() =>
        View("SourceForm", new SourceInput { Connector = ConnectorKind.Automatic });

    // Preserve old source links while new forms use explicit Create/Edit actions.
    [HttpGet]
    public IActionResult Source(int? id) =>
        id is null ? RedirectToAction(nameof(Create)) : RedirectToAction(nameof(Edit), new { id });

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var source = await management.ReadAsync(id.Value, ct);
        return source is null ? NotFound() : View("SourceForm", SourceInput.From(source));
    }

    [HttpPost]
    public Task<IActionResult> Create(
        [Bind(
            "Id,Name,Url,Connector,Enabled,UsageApproved,ApprovalNote,ConfigurationJson,IntervalHours,CollectionStartTime,Version"
        )]
            SourceInput input,
        CancellationToken ct,
        SourceClock? clock = null
    )
    {
        if (input.Id != 0)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct, clock);
    }

    [HttpPost]
    public Task<IActionResult> Edit(
        [FromRoute] int id,
        [Bind(
            "Id,Name,Url,Connector,Enabled,UsageApproved,ApprovalNote,ConfigurationJson,IntervalHours,CollectionStartTime,Version"
        )]
            SourceInput input,
        CancellationToken ct,
        SourceClock? clock = null
    )
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct, clock);
    }

    private async Task<IActionResult> SaveAsync(
        SourceInput input,
        CancellationToken ct,
        SourceClock? clock
    )
    {
        if (clock is not null)
        {
            if (!TryValidateModel(clock, "Clock"))
                return View("SourceForm", input);
            input.CollectionStartTime = clock.ToTime();
        }
        NormalizeStrings(input);
        if (!ModelState.IsValid) return View("SourceForm", input);
        var result = await management
            .SaveAsync(input.ToDto(), User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == SourceSaveOutcome.NotFound) return NotFound();
        if (result.Outcome == SourceSaveOutcome.Duplicate) ViewData["DuplicateSourceName"] = result.DuplicateName;
        if (result.Outcome != SourceSaveOutcome.Saved)
        {
            foreach (var error in result.Errors ?? []) ModelState.AddModelError("", error);
            return View("SourceForm", input);
        }
        return RedirectToAction(nameof(Sources));
    }

    [HttpPost]
    public async Task<IActionResult> CollectNow(int id, CancellationToken ct)
    {
        var notice = await collectionRequests
            .QueueAsync(id, User.Identity?.Name ?? "Admin", ct);
        if (notice is null) return NotFound();
        TempData["Notice"] = notice;
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
        if (ApplicationRuntime.Mode(configuration) != ApplicationRuntimeMode.Local)
            return StatusCode(409, new { error = "SourceSetupLocalOnly" });
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

    /// <summary>يفوض كشف العنوان المكرر إلى خدمة المصادر.</summary>
    private Task<SourceDuplicateSummaryDto?> ExistingSourceAsync(string url, int excludedId, CancellationToken ct) =>
        management.FindDuplicateAsync(url, excludedId, ct);
}
