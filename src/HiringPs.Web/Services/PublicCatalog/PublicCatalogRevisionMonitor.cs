using System.Data.Common;
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Models;

/// <summary>يراقب إصدار القاعدة أثناء الزيارات، باستعلام واحد لكل خمس ثوانٍ مهما تعددت الطلبات.</summary>
public sealed class PublicCatalogRevisionMonitor(
    IServiceScopeFactory scopes, PublicCatalogCache cache, ILogger<PublicCatalogRevisionMonitor> logger,
    TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private long nextCheckTicks;
    private long? observed;

    /// <summary>يبطل كاش هذه العملية عند وصول تغييرات عامل آخر؛ عند تعذر القاعدة يعيد المحاولة بعد المهلة القصيرة.</summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        if (clock.GetUtcNow().UtcTicks < Volatile.Read(ref nextCheckTicks) || !await gate.WaitAsync(0, ct)) return;
        try
        {
            if (clock.GetUtcNow().UtcTicks < nextCheckTicks) return;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
            var version = await db.PublicCatalogRevisions.AsNoTracking().Where(x => x.Id == 1)
                .Select(x => x.Version).SingleAsync(budget.Token);
            if (observed is { } old && old != version) cache.Invalidate();
            observed = version;
        }
        catch (Exception ex) when (ex is DbException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Public catalog revision check failed ({Type}); retrying on a later visit.", ex.GetType().Name);
        }
        finally
        {
            Volatile.Write(ref nextCheckTicks, clock.GetUtcNow().AddSeconds(5).UtcTicks);
            gate.Release();
        }
    }
}
