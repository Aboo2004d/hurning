using System.Globalization;
using HiringPs.Core;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

public partial class AdminController
{
    private static readonly SemaphoreSlim SourceSetupGate = new(1, 1);

    [HttpPost]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> SuggestSource(
        string? url,
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
            var result = await assistant.SuggestAsync(
                url?.Trim() ?? "",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                budget.Token
            );
            Audit(
                result.Verified ? "Source.SetupPreview" : "Source.SetupProposed",
                new Uri(result.Url).IdnHost
            );
            await db.SaveChangesAsync(budget.Token);
            return Json(result);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(408, new { error = "SourceSetupTimeout" });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
            return BadRequest(
                new { error = codes.Contains(ex.Message) ? ex.Message : "SourceSetupFailed" }
            );
        }
        finally
        {
            SourceSetupGate.Release();
        }
    }
}
