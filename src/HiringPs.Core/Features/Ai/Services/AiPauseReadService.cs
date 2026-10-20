using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة سبب ووقت توقف AI للإدارة دون إنشاء صف أو تغيير موعد الاستئناف.</summary>
public sealed class AiPauseReadService(HiringDb db)
{
    /// <summary>يعيد حالة الحاكم للعرض؛ غياب صف الحالة يعني عدم وجود إيقاف مسجل.</summary>
    public async Task<AiPauseStatusDto> ReadAsync(CancellationToken ct) =>
        await db.PipelineStates.AsNoTracking().Where(x => x.Id == 1)
            .Select(x => new AiPauseStatusDto(x.AiPausedUntil, x.AiPauseReason))
            .SingleOrDefaultAsync(ct) ?? new AiPauseStatusDto(null, "");
}
