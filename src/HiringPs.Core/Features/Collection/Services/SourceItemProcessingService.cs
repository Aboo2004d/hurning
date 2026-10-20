// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يعالج عنصرًا واحدًا مع بصمة المحتوى وحصص AI وإعادة استخدام الاستخراج وسياسات المراجعة.</summary>
public sealed class SourceItemProcessingService(HiringDb itemDb, BrowserCollector itemBrowser,
    AiExtractor ai, OpportunityService processor, IConfiguration config, ILogger<CollectionPipeline> log)
{
    /// <summary>يحفظ التقدم ويرجع حالة إيقاف AI؛ لا يعيد تشغيل الاستخراج المدفوع المحفوظ.</summary>
    public async Task<SourceItemProcessingResultDto> ProcessAsync(long itemId, PromptVersion activePrompt,
        DateTimeOffset? aiPausedUntil, Func<CancellationToken, Task> verifyLease,
        Func<DateTimeOffset, CancellationToken, Task<DateTimeOffset>> pauseForBudget, CancellationToken ct)
    {
        var item = await itemDb
            .SourceItems.Include(x => x.Source)
            .SingleAsync(x => x.Id == itemId, ct);
        var stopAi = false;
        try
        {
            var linked = await itemDb
                .SourceLinks.AsNoTracking()
                .Include(x => x.Opportunity)
                .Where(x => x.SourceItemId == item.Id)
                .ToListAsync(ct);
            if (
                linked.Count > 0
                && linked.All(x => PromptWorkflow.Protected(x.Opportunity, activePrompt.Id))
            )
            {
                item.Status = ProcessingStatus.Published;
                item.RetryAt = null;
                // Protected decisions are not re-fetched; wait a full interval before checking again.
                item.LastVerifiedAt = DateTimeOffset.UtcNow;
                await itemDb.SaveChangesAsync(ct);
                return new(aiPausedUntil, false);
            }
            if (item.Status != ProcessingStatus.AwaitingAi)
            {
                var bundle = await itemBrowser.FetchBundleAsync(item.Source, item.Url, ct);
                var content = bundle.Text;
                var hash = ContentFingerprint.Hash(content);
                await verifyLease(ct);
                item.LastVerifiedAt = DateTimeOffset.UtcNow;
                // Unchanged main text with a saved extraction for this prompt: no new AI call.
                if (
                    ContentFingerprint.Matches(item.ContentHash, content)
                    && item.ExtractionJson.Length > 0
                    && item.ExtractionPromptVersionId == activePrompt.Id
                )
                {
                    var saved =
                        JsonSerializer.Deserialize<ExtractionResult>(
                            item.ExtractionJson,
                            AiExtractor.JsonOptions
                        )
                        ?? throw new ContentNeedsReviewException(
                            "Saved extraction is unreadable; original text retained for review."
                        );
                    saved.PromptVersionId = item.ExtractionPromptVersionId;
                    item.ContentHash = hash;
                    await processor.ApplyExtractionAsync(item, saved, ct);
                    item.RetryAt = null;
                    item.Attempts = 0;
                    await itemDb.SaveChangesAsync(ct);
                    return new(aiPausedUntil, false);
                }

                if (item.Content.Length > 0)
                    itemDb.Revisions.Add(
                        new()
                        {
                            SourceItemId = item.Id,
                            ContentHash = item.ContentHash,
                            Content = item.Content,
                        }
                    );
                item.Content = content;
                item.ContentHash = hash;
                item.AttachmentsJson = JsonSerializer.Serialize(bundle.Attachments);
                item.Status = ProcessingStatus.AwaitingAi;
                item.ExtractionJson = "";
                await itemDb.SaveChangesAsync(ct);
            }

            if (aiPausedUntil is null && await AiGovernor.BudgetExceededAsync(itemDb, config, DateTimeOffset.UtcNow, ct))
                aiPausedUntil = await pauseForBudget(DateTimeOffset.UtcNow, ct);
            if (aiPausedUntil is { } pausedUntil)
            {
                item.Status = ProcessingStatus.AwaitingAi;
                item.RetryAt = pausedUntil;
                item.Error =
                    $"AI paused until {SourceSchedule.Display(pausedUntil)} (Palestine time); content saved.";
                await itemDb.SaveChangesAsync(ct);
                return new(aiPausedUntil, false);
            }

            var result = await ai.ExtractAsync(
                SourceEvidence.ForItem(item),
                ct,
                new AiExecution(activePrompt, item.Id)
            );
            await verifyLease(ct);
            // Save the paid result with its prompt version before applying, so a failed
            // apply reuses it instead of calling the model again.
            item.ExtractionJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
            item.ExtractionPromptVersionId = result.PromptVersionId;
            await itemDb.SaveChangesAsync(ct);
            await processor.ApplyExtractionAsync(item, result, ct);
            item.RetryAt = null;
            item.Attempts = 0;
        }
        catch (AiQuotaException ex)
        {
            // Quota or credentials: pause every AI call globally and stop this item loop.
            var until = DateTimeOffset.UtcNow + AiGovernor.QuotaPause(ex, config);
            await AiGovernor.PauseAsync(itemDb, until, ex.Message, ct);
            aiPausedUntil = until;
            stopAi = true;
            item.Status = ProcessingStatus.AwaitingAi;
            item.Error = ex.Message;
            item.RetryAt = until;
            log.LogWarning(
                "AI paused until {Until} after provider refusal.",
                SourceSchedule.Display(until)
            );
        }
        catch (AiUnavailableException ex)
        {
            item.Status = ProcessingStatus.AwaitingAi;
            item.Error = ex.Message;
            item.RetryAt = DateTimeOffset.UtcNow.AddHours(6);
        }
        catch (AiTransientException ex)
        {
            item.Attempts++;
            if (item.Attempts >= AiGovernor.MaxTransientAttempts(config))
            {
                item.Status = ProcessingStatus.NeedsReview;
                item.Error = ("AI provider kept failing; saved content retained. " + ex.Message).Truncate(1800);
            }
            else
            {
                item.Status = ProcessingStatus.AwaitingAi;
                item.Error = ex.Message;
                item.RetryAt = DateTimeOffset.UtcNow + AiGovernor.TransientBackoff(item.Attempts);
            }
        }
        catch (CollectorDiagnosticException ex)
        {
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            itemDb.Audit.Add(
                new()
                {
                    Actor = "Collector",
                    Action = "Collector.Diagnostic",
                    Detail = CollectorDiagnosticFiles.AuditDetail(ex.Diagnostic, itemId),
                }
            );
            if (ex.InnerException is ContentNeedsReviewException)
                item.Status = ProcessingStatus.NeedsReview;
            else
                ScheduleRetry(item);

            item.Error = CollectionErrorRules.SafeError(ex);
        }
        catch (ContentNeedsReviewException ex)
        {
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            item.Status = ProcessingStatus.NeedsReview;
            item.Error = ex.Message.Truncate(1800);
        }
        catch (JsonException)
        {
            // Saved or returned extraction cannot be read; repeating it would not change that.
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            item.Status = ProcessingStatus.NeedsReview;
            item.Error = "Extraction JSON is unreadable; original text retained for review.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            ScheduleRetry(item);
            item.Error = CollectionErrorRules.SafeError(ex);
        }

        await verifyLease(ct);
        try
        {
            await itemDb.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // One item's failed status save (e.g. a concurrent admin edit) must not stop the tick.
            log.LogWarning("Item {Id} status save failed: {Type}", itemId, ex.GetType().Name);
        }
        return new(aiPausedUntil, stopAi);
    }

    /// <summary>يحسب تراجع إعادة المحاولة وينقل العنصر للمراجعة بعد الحد المعتمد.</summary>
    private static void ScheduleRetry(SourceItem item)
    {
        item.Attempts++;
        item.Status = item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
        item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(240, 10 * item.Attempts));
    }

}
