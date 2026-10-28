using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة قائمة المصادر وتقدم الاكتشاف عبر حقول عرض محددة.</summary>
public sealed class SourceReadService(HiringDb db)
{
    /// <summary>يسقط المصدر وحالات طلبات جمعه قبل ترقيم القائمة بنفس ترتيب المصادر الحالي.</summary>
    public IQueryable<SourceSummaryDto> Sources() => db.Sources.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new SourceSummaryDto
        {
            Id = x.Id, Name = x.Name, Enabled = x.Enabled, UsageApproved = x.UsageApproved,
            CollectionStartTime = x.CollectionStartTime, IntervalHours = x.IntervalHours,
            NextDueAt = x.NextDueAt, LastError = x.LastError, HealthWarning = x.HealthWarning,
            CollectionRequests = x.CollectionRequests.Select(request =>
                new SourceCollectionRequestSummaryDto(request.Status, request.Error)).ToList(),
        });

    /// <summary>يفضل العامل النشط الحديث على نسخة خاملة، ويعتبر النبضات المنقطعة غير متصلة.</summary>
    public async Task<CollectionWorkerStatusDto> WorkerStatusAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = now.AddSeconds(-60);
        var newest = now.AddSeconds(30);
        var worker = await db.ToolWorkers.AsNoTracking()
            .OrderByDescending(x => x.At >= oldest && x.At <= newest)
            .ThenByDescending(x => x.At >= oldest && x.At <= newest && x.Collecting)
            .ThenByDescending(x => x.At)
            .FirstOrDefaultAsync(ct);
        var connected = ToolMonitoringRules.WorkerFresh(worker, now);
        return new(connected, connected && worker!.Collecting, worker?.At);
    }

    /// <summary>يعيد عدادات الصفحات الموجودة في صفحة الإدارة فقط.</summary>
    public Task<Dictionary<int, DiscoveryProgressSummaryDto>> ProgressAsync(int[] ids, CancellationToken ct) =>
        db.DiscoveryCheckpoints.AsNoTracking().Where(x => ids.Contains(x.SourceId))
            .Select(x => new DiscoveryProgressSummaryDto(x.SourceId, x.CompletedPages, x.Found))
            .ToDictionaryAsync(x => x.SourceId, ct);
}
