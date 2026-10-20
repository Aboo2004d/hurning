using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads worker metrics independently of MVC and without loading tracked entity graphs.</summary>
public sealed class AdminDashboardReadService(HiringDb db)
{
    /// <summary>Projects collection run rows in their existing newest-id ordering.</summary>
    public IQueryable<ScrapeRunSummaryDto> Runs() => db.Runs.AsNoTracking().OrderByDescending(x => x.Id)
        .Select(x => new ScrapeRunSummaryDto(x.Id, x.SourceId, x.StartedAt, x.Status, x.Found, x.Changed, x.Error));

    /// <summary>Reads the first heartbeat by id, matching the existing dashboard selection.</summary>
    public Task<WorkerHeartbeatSummaryDto?> HeartbeatAsync(CancellationToken ct) => db.Heartbeats.AsNoTracking()
        .OrderBy(x => x.Id).Select(x => new WorkerHeartbeatSummaryDto(x.At, x.State)).FirstOrDefaultAsync(ct);

    /// <summary>Counts each dashboard state using the original independent database queries.</summary>
    public async Task<AdminDashboardCountsDto> CountsAsync(CancellationToken ct) => new(
        await db.Opportunities.CountAsync(x => x.Status == PublicationStatus.Published, ct),
        await db.Opportunities.CountAsync(x => x.Status == PublicationStatus.NeedsReview, ct),
        await db.SourceItems.CountAsync(x => x.Status == ProcessingStatus.Discovered || x.Status == ProcessingStatus.AwaitingAi || x.Status == ProcessingStatus.Retry, ct),
        await db.Reports.CountAsync(x => !x.Resolved, ct));
}
