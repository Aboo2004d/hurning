using System.Globalization;
using HiringPs.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public partial class AdminController
{
    private static readonly SemaphoreSlim SourceSetupGate = new(1, 1);

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
            return StatusCode(408, new { error = "SourceSetupTimeout", detail = SourceSetupErrorDetail(ex),
                stage = ex.Data["SourceSetupStage"] as string, reference = HttpContext.TraceIdentifier });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<SourceSetupAssistant>>()
                .LogWarning(ex, "Source setup failed for {Host}; stage {Stage}; request {RequestId}.",
                    Uri.TryCreate(url, UriKind.Absolute, out var target) ? target.IdnHost : "invalid",
                    ex.Data["SourceSetupStage"] as string ?? "SourceSetupStageConfiguration",
                    HttpContext.TraceIdentifier);
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
            var original = ex is CollectorDiagnosticException diagnostic ? diagnostic.InnerException ?? ex : ex;
            var error = codes.Contains(ex.Message) ? ex.Message
                : original is TimeoutException ? "SourceSetupTimeout"
                : original is System.Net.Http.HttpRequestException ? "SourceSetupNetworkFailed"
                : original is System.Text.Json.JsonException ? "SourceSetupInvalidReply"
                : original is ContentNeedsReviewException ? "SourceSetupSampleInvalid"
                : original is Microsoft.Playwright.PlaywrightException ? "SourceSetupBrowserFailed"
                : "SourceSetupFailed";
            return BadRequest(new { error, detail = SourceSetupErrorDetail(ex), stage = ex.Data["SourceSetupStage"] as string,
                reference = HttpContext.TraceIdentifier });
        }
        finally
        {
            SourceSetupGate.Release();
        }
    }

    private string SourceSetupErrorDetail(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null && messages.Count < 8; current = current.InnerException)
            messages.Add($"{current.GetType().Name}: {current.Message}");
        var detail = string.Join("\n", messages);
        foreach (var setting in configuration.AsEnumerable().Where(s =>
            s.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(s.Key, @"(?:^|:)(?:ApiKey|SecretKey|Secret|Password|Token|ClientSecret)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            if (!string.IsNullOrEmpty(setting.Value) && setting.Value.Length >= 4)
                detail = detail.Replace(setting.Value, "[REDACTED]", StringComparison.Ordinal);
        detail = System.Text.RegularExpressions.Regex.Replace(detail,
            @"(?i)(\b(?:api[_-]?key|access_token|token|password|pwd|client_secret)\s*[=:]\s*)[^\s&;""<>]+", "$1[REDACTED]");
        detail = System.Text.RegularExpressions.Regex.Replace(detail,
            @"(https?://)[^/\s:@]+:[^/\s@]+@", "$1[REDACTED]@");
        return detail.Length <= 12000 ? detail : detail[..12000];
    }

    private async Task<Source?> ExistingSourceAsync(string url, int excludedId, CancellationToken ct)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var requested)) return null;
        var sources = await db.Sources.AsNoTracking().Where(s => s.Id != excludedId).ToListAsync(ct);
        return sources.FirstOrDefault(s => Uri.TryCreate(s.Url, UriKind.Absolute, out var saved)
            && Uri.Compare(requested, saved, UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
                UriFormat.UriEscaped, StringComparison.Ordinal) == 0);
    }
}
