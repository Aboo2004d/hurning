using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يعالج عنصر مصدر واحد ضمن نطاق مستقل ويحفظ الاستخراج وسياسة إعادة المحاولة.</summary>
public sealed class SourceItemProcessingService(
    HiringDb itemDb, BrowserCollector itemBrowser, AiExtractor ai, OpportunityService processor)
{
    /// <summary>يجلب النص ويعيد استخدام الاستخراج أو ينفذ AI؛ يفحص ملكية قفل الجمع قبل الحفظ الحاسم.</summary>
    public async Task ProcessAsync(long itemId, PromptVersion activePrompt,
        Func<CancellationToken, Task> verifyLease, CancellationToken ct)
    {
        var item = await itemDb
            .SourceItems.Include(x => x.Source)
            .SingleAsync(x => x.Id == itemId, ct);
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
                await itemDb.SaveChangesAsync(ct);
                return;
            }
            if (item.Status != ProcessingStatus.AwaitingAi)
            {
                var bundle = await itemBrowser.FetchBundleAsync(item.Source, item.Url, ct);
                var content = bundle.Text;
                var hash = TextRules.Hash(content);
                await verifyLease(ct);
                if (
                    item.ContentHash == hash
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
                    await processor.ApplyExtractionAsync(item, saved, ct);
                    item.RetryAt = null;
                    await itemDb.SaveChangesAsync(ct);
                    return;
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

            var result = await ai.ExtractAsync(
                SourceEvidence.ForItem(item),
                ct,
                new AiExecution(activePrompt, item.Id)
            );
            await verifyLease(ct);
            item.ExtractionJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
            await itemDb.SaveChangesAsync(ct);
            await processor.ApplyExtractionAsync(item, result, ct);
            item.RetryAt = null;
        }
        catch (AiUnavailableException ex)
        {
            item.Status = ProcessingStatus.AwaitingAi;
            item.Error = ex.Message;
            item.RetryAt = DateTimeOffset.UtcNow.AddHours(6);
        }
        catch (AiTransientException ex)
        {
            item.Status = ProcessingStatus.AwaitingAi;
            item.Error = ex.Message;
            item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(15);
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
            {
                item.Attempts++;
                item.Status =
                    item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
                item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(
                    Math.Min(240, 10 * item.Attempts)
                );
            }

            item.Error = CollectionErrorRules.SafeError(ex);
        }
        catch (ContentNeedsReviewException ex)
        {
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            item.Status = ProcessingStatus.NeedsReview;
            item.Error = ex.Message;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            itemDb.ChangeTracker.Clear();
            item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
            item.Attempts++;
            item.Status =
                item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
            item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(240, 10 * item.Attempts));
            item.Error = CollectionErrorRules.SafeError(ex);
        }

        await verifyLease(ct);
        await itemDb.SaveChangesAsync(ct);
    }
}
