using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحفظ صفحة اكتشاف ومؤشر استئنافها وعدادات جولتها في معاملة واحدة.</summary>
public sealed class DiscoveryBatchStore(HiringDb pageDb)
{
    /// <summary>يثبت الدفعة داخل نطاق الصفحة؛ لا يحفظ تغييرات من نطاق دورة الجمع.</summary>
    public async Task SaveAsync(int sourceId, ConnectorKind connector, long runId,
        int promptVersionId, DiscoveryBatch batch, CancellationToken token)
    {
        var savedCheckpoint = await pageDb.DiscoveryCheckpoints.SingleAsync(
            x => x.SourceId == sourceId,
            token
        );
        var savedRun = await pageDb.Runs.SingleAsync(x => x.Id == runId, token);
        await using var pageTransaction =
            await pageDb.Database.BeginTransactionAsync(token);
        foreach (var entry in batch.Items.DistinctBy(x => x.Id))
        {
            var item = await pageDb.SourceItems.SingleOrDefaultAsync(
                x => x.SourceId == sourceId && x.ExternalId == entry.Id,
                token
            );
            if (item is null)
            {
                pageDb.SourceItems.Add(
                    new()
                    {
                        SourceId = sourceId,
                        ExternalId = entry.Id,
                        Url = entry.Url,
                        Status = ProcessingStatus.Discovered,
                        RequestedPromptVersionId = promptVersionId,
                    }
                );
                savedRun.Changed++;
            }
            else
            {
                item.LastSeenAt = DateTimeOffset.UtcNow;
                if (item.Status == ProcessingStatus.Published)
                {
                    item.Status = ProcessingStatus.Discovered;
                    item.RetryAt = null;
                }
            }
        }
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
}
