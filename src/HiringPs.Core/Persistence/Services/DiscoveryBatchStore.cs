// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يثبت دفعة اكتشاف ومؤشرها وعداداتها مع سياسة إعادة التحقق الجديدة.</summary>
public sealed class DiscoveryBatchStore(HiringDb pageDb)
{
    /// <summary>يحفظ الصفحة ضمن معاملة واحدة مع تحميل عناصرها السابق دفعة واحدة.</summary>
    public async Task SaveAsync(int sourceId, ConnectorKind connector, long runId,
        int activePromptId, DateTimeOffset reverifyBefore, DiscoveryBatch batch, CancellationToken token)
    {
        var savedCheckpoint = await pageDb.DiscoveryCheckpoints.SingleAsync(
            x => x.SourceId == sourceId,
            token
        );
        var savedRun = await pageDb.Runs.SingleAsync(x => x.Id == runId, token);
        await using var pageTransaction =
            await pageDb.Database.BeginTransactionAsync(token);
        await SaveDiscoveredAsync(
            pageDb,
            sourceId,
            batch,
            savedRun,
            activePromptId,
            reverifyBefore,
            token
        );
        var visited =
            JsonSerializer.Deserialize<List<string>>(
                savedCheckpoint.VisitedUrlsJson
            ) ?? [];
        if (
            connector == ConnectorKind.Automatic
            && savedCheckpoint.NextUrl.Length > 0
        )
            visited.Add(savedCheckpoint.NextUrl);
        savedCheckpoint.VisitedUrlsJson = JsonSerializer.Serialize(
            visited.Distinct()
        );
        savedCheckpoint.CompletedPages = batch.CompletedPages;
        savedCheckpoint.NextUrl = batch.NextUrl ?? "";
        savedCheckpoint.ReplayClicks = batch.ReplayClicks;
        savedCheckpoint.Completed = batch.NextUrl is null;
        savedCheckpoint.Found += batch.Items.Count;
        savedCheckpoint.UpdatedAt = DateTimeOffset.UtcNow;
        savedRun.Found = savedCheckpoint.Found;
        await pageDb.SaveChangesAsync(token);
        await pageTransaction.CommitAsync(token);
    }

    /// <summary>يحمل عناصر الصفحة دفعة واحدة ويعيد جلب المنشور فقط بعد مهلة التحقق.</summary>
    private static async Task SaveDiscoveredAsync(
        HiringDb pageDb,
        int sourceId,
        DiscoveryBatch batch,
        ScrapeRun savedRun,
        int activePromptId,
        DateTimeOffset reverifyBefore,
        CancellationToken token
    )
    {
        var entries = batch.Items.DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
        var ids = entries.Select(x => x.Id).ToArray();
        // One lookup per page instead of one query per listing row.
        var existing = await pageDb
            .SourceItems.Where(x => x.SourceId == sourceId && ids.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.OrdinalIgnoreCase, token);
        var seen = DateTimeOffset.UtcNow;
        foreach (var entry in entries)
        {
            if (!existing.TryGetValue(entry.Id, out var item))
            {
                pageDb.SourceItems.Add(
                    new()
                    {
                        SourceId = sourceId,
                        ExternalId = entry.Id,
                        Url = entry.Url,
                        Status = ProcessingStatus.Discovered,
                        RequestedPromptVersionId = activePromptId,
                    }
                );
                savedRun.Changed++;
                continue;
            }
            item.LastSeenAt = seen;
            // Published items are re-fetched only once their last verification is old enough.
            if (
                item.Status == ProcessingStatus.Published
                && (item.LastVerifiedAt is null || item.LastVerifiedAt < reverifyBefore)
            )
            {
                item.Status = ProcessingStatus.Discovered;
                item.RetryAt = null;
            }
        }
    }

}
