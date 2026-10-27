using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>ينفذ طلبات الفحص المعلقة وقت الأمر فقط، ويتوقف إذا امتلك عامل آخر قفل التنفيذ.</summary>
public static class LocalToolCheckRunner
{
    /// <summary>ينفذ لقطة محدودة من الطلبات ويغلق الطلبات المنقطعة دون إعادة الفحوص المدفوعة تلقائيًا.</summary>
    public static async Task<int> DrainAsync(IServiceProvider services, CancellationToken ct)
    {
        long ceiling;
        await using (var initial = services.CreateAsyncScope())
            ceiling = await initial.ServiceProvider.GetRequiredService<HiringDb>().ToolCheckRequests
                .Where(x => (x.Status == ToolRequestStatus.Pending || x.Status == ToolRequestStatus.Running)).MaxAsync(x => (long?)x.Id, ct) ?? 0;
        var count = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
            var id = await db.ToolCheckRequests.Where(x => (x.Status == ToolRequestStatus.Pending || x.Status == ToolRequestStatus.Running) && x.Id <= ceiling)
                .OrderBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
            if (id is null) return count;
            await ToolCheckQueue.RunNextAsync(db, scope.ServiceProvider.GetRequiredService<IConfiguration>(),
                scope.ServiceProvider.GetRequiredService<ToolWorkerRuntime>(), scope.ServiceProvider, ct, maximumRequestId: ceiling);
            if (await db.ToolCheckRequests.AsNoTracking().AnyAsync(x => x.Id == id && (x.Status == ToolRequestStatus.Pending || x.Status == ToolRequestStatus.Running), ct)) return count;
            count++;
        }
    }
}
