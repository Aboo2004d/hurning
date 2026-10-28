using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public partial class CollectionPipeline
{
    /// <summary>يجمع كل المصادر المعتمدة أو مصدرًا محددًا مرة واحدة تحت قفل الجولة، دون تجاهل حدود AI أو إعادة الخصم.</summary>
    public async Task<ManualCollectionSummaryDto> CollectManualAsync(int? sourceId, CancellationToken ct)
    {
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct)) throw new InvalidOperationException("Another collection round already owns the pipeline lock.");
        return await CollectUnderLockAsync(sourceId, gate, ct);
    }

    /// <summary>يلتقط طلبًا يدويًا واحدًا تحت قفل الجولة؛ لا يجمع المصادر المجدولة أو يبدأ العمل دون طلب.</summary>
    public async Task<ManualCollectionSummaryDto?> TryCollectQueuedAsync(CancellationToken ct)
    {
        // A short read avoids opening the lock connection while the queue is empty.
        if (!await db.Set<SourceCollectionRequest>().AnyAsync(x =>
            (x.Status == "Pending" || x.Status == "Running")
            && x.Source.Enabled && x.Source.UsageApproved && x.Source.ApprovalNote != "", ct))
            return null;
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct)) return null;
        // Recheck after acquiring the lock: another collector may have finished this request.
        var id = await db.Set<SourceCollectionRequest>().AsNoTracking()
            .Where(x => (x.Status == "Pending" || x.Status == "Running")
                && x.Source.Enabled && x.Source.UsageApproved && x.Source.ApprovalNote != "")
            .OrderBy(x => x.RequestedAt).ThenBy(x => x.SourceId)
            .Select(x => (int?)x.SourceId).FirstOrDefaultAsync(ct);
        if (id is null) return null;
        using var runtimeScope = scopes.CreateScope();
        var runtime = runtimeScope.ServiceProvider.GetRequiredService<ToolWorkerRuntime>();
        runtime.Begin();
        try { return await CollectUnderLockAsync(id, gate, ct); }
        finally { runtime.Stop(); }
    }

    /// <summary>ينفذ الجولة بالقفل المكتسب نفسه لإعادة استخدام التدقيق والحدود ونقاط الاستئناف.</summary>
    private async Task<ManualCollectionSummaryDto> CollectUnderLockAsync(int? sourceId, SqlRunLock gate, CancellationToken ct)
    {
        var selected = db.Sources.Where(x => x.Enabled && x.UsageApproved && x.ApprovalNote != "");
        if (sourceId is not null) selected = selected.Where(x => x.Id == sourceId);
        var ids = await selected.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        if (sourceId is not null && ids.Count == 0)
            throw new InvalidOperationException("The requested source is missing, disabled or not approved for collection.");
        var now = DateTimeOffset.UtcNow;
        await db.Set<SourceCollectionRequest>().Where(x => (x.Status == "Pending" || x.Status == "Running")
            && (!x.Source.Enabled || !x.Source.UsageApproved || x.Source.ApprovalNote == ""))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Cancelled")
                .SetProperty(x => x.Error, "Source disabled or usage approval removed.")
                .SetProperty(x => x.FinishedAt, (DateTimeOffset?)now), ct);
        var heartbeat = await db.Heartbeats.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (heartbeat is null) { heartbeat = new(); db.Heartbeats.Add(heartbeat); }
        heartbeat.At = DateTimeOffset.UtcNow;
        heartbeat.State = "Running";
        await db.SaveChangesAsync(ct);
        var finalState = "Failed";
        try
        {
            var prompt = await PromptWorkflow.ActivateAsync(db, ct);
            await RetentionCleanup.RunAsync(db, config, ct);
            db.Audit.Add(OperationTracking.Create(OperationArea.Collection, OperationOutcome.Started,
                "ManualRound", $"Sources {ids.Count}; Selected {sourceId?.ToString() ?? "all"}", "Local collector"));
            await db.SaveChangesAsync(ct);
            var failed = 0;
            var processed = 0;
            var pending = 0;
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                await gate.VerifyAsync(ct);
                // Reload permissions immediately before each source; removed approval is never overridden by the CLI.
                var source = await db.Sources.SingleAsync(x => x.Id == id, ct);
                await db.Entry(source).ReloadAsync(ct);
                if (!source.Enabled || !source.UsageApproved || source.ApprovalNote.Length == 0) continue;
                var request = await db.Set<SourceCollectionRequest>().FindAsync([id], ct);
                if (request is null) { request = new() { SourceId = id }; db.Add(request); }
                var resume = request.Status is "Pending" or "Running";
                if (!resume)
                {
                    request.RequestToken = Guid.NewGuid();
                    request.RequestedAt = DateTimeOffset.UtcNow;
                    request.StartedAt = null;
                    request.DiscoveryFinished = false;
                }
                request.Status = "Running";
                request.StartedAt ??= DateTimeOffset.UtcNow;
                request.FinishedAt = null;
                request.Error = "";
                await db.SaveChangesAsync(ct);
                log.LogInformation("Manual collection source {SourceId} started.", id);
                if (!request.DiscoveryFinished)
                    await new SourceDiscoveryService(db, browser, config, log, scopes).DiscoverAsync(gate, source, request, prompt, ct);
                if (request.Status == "Failed") { failed++; continue; }
                var maximum = await db.SourceItems.Where(x => x.SourceId == id).MaxAsync(x => (long?)x.Id, ct) ?? 0;
                long cursor = 0;
                while (cursor < maximum)
                {
                    var batch = await ProcessPendingAsync(gate, request, prompt, ct, cursor, maximum);
                    processed += batch.Count;
                    if (batch.Count == 0 || batch.Stop) break;
                    cursor = batch.LastId;
                }
                var remaining = await db.SourceItems.CountAsync(x => x.SourceId == id
                    && (x.Status == ProcessingStatus.Discovered || x.Status == ProcessingStatus.AwaitingAi || x.Status == ProcessingStatus.Retry), ct);
                    request.Status = "Completed";
                request.FinishedAt = DateTimeOffset.UtcNow;
                request.Error = $"Manual discovery finished; pending processing/AI/retries: {remaining}. Pending items are not published successes.";
                await opportunities.ArchiveExpiredAsync(ct, id);
                await db.SaveChangesAsync(ct);
                log.LogInformation("Manual collection source {SourceId} finished; pending items {Pending}.", id, remaining);
            }
            pending = ids.Count == 0 ? 0 : await db.SourceItems.CountAsync(x => ids.Contains(x.SourceId)
                && (x.Status == ProcessingStatus.Discovered || x.Status == ProcessingStatus.AwaitingAi || x.Status == ProcessingStatus.Retry), ct);
            db.Audit.Add(OperationTracking.Create(OperationArea.Collection, failed > 0 ? OperationOutcome.Failed : OperationOutcome.Succeeded,
                "ManualRoundFinished", $"Sources {ids.Count}; Failed {failed}; Processed {processed}; Pending {pending}", "Local collector"));
            await db.SaveChangesAsync(ct);
            finalState = failed == 0 ? "Idle" : "Failed";
            return new(ids.Count, failed, processed, pending);
        }
        finally { await FinishHeartbeatAsync(heartbeat.Id, finalState); }
    }
}
