// إيقاف استدعاءات الذكاء الاصطناعي مؤقتًا عند رفض المزوّد أو تجاوز ميزانية الرموز اليومية.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>مكون AiGovernor ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class AiGovernor
{
    public const long DefaultDailyTokenBudget = 20_000_000;

    /// <summary>يقرأ أو ينشئ حالة الحاكم المشتركة بين دورات العامل.</summary>
    public static async Task<PipelineState> StateAsync(HiringDb db, CancellationToken ct)
    {
        var state = await db.PipelineStates.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (state is not null)
            return state;
        state = new PipelineState { Id = 1 };
        db.PipelineStates.Add(state);
        await db.SaveChangesAsync(ct);
        return state;
    }

    /// <summary>يتحقق من مهلة الإيقاف العام الحالية لاستدعاءات AI.</summary>
    public static async Task<DateTimeOffset?> PausedUntilAsync(
        HiringDb db,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var until = await db
            .PipelineStates.AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.AiPausedUntil)
            .FirstOrDefaultAsync(ct);
        return until > now ? until : null;
    }

    // Extends an existing pause, never shortens it.
    /// <summary>يثبت موعد الإيقاف العام وسببه دون تسجيل بيانات الاعتماد.</summary>
    public static async Task PauseAsync(
        HiringDb db,
        DateTimeOffset until,
        string reason,
        CancellationToken ct
    )
    {
        await StateAsync(db, ct);
        await db
            .PipelineStates.Where(x =>
                x.Id == 1 && (x.AiPausedUntil == null || x.AiPausedUntil < until)
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.AiPausedUntil, (DateTimeOffset?)until)
                        .SetProperty(x => x.AiPauseReason, reason.Truncate(300)),
                ct
            );
    }

    /// <summary>يحدد مدة التوقف حسب رفض الحصة أو بيانات الاعتماد وإعدادات النظام.</summary>
    public static TimeSpan QuotaPause(AiQuotaException error, IConfiguration config)
    {
        if (error.Credentials)
            return TimeSpan.FromMinutes(
                Math.Clamp(config.GetValue<int?>("Ai:CredentialPauseMinutes") ?? 360, 5, 1440)
            );
        if (error.RetryAfter is { } retry && retry > TimeSpan.Zero)
            return retry < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1)
                : retry > TimeSpan.FromHours(24) ? TimeSpan.FromHours(24)
                : retry;
        return TimeSpan.FromMinutes(
            Math.Clamp(config.GetValue<int?>("Ai:QuotaPauseMinutes") ?? 60, 1, 1440)
        );
    }

    // 15 minutes, doubling per attempt, capped at 12 hours.
    /// <summary>يحسب مهلة تراجع الخطأ العابر وفق عدد المحاولات.</summary>
    public static TimeSpan TransientBackoff(int attempts) =>
        TimeSpan.FromMinutes(Math.Min(720, 15 * Math.Pow(2, Math.Clamp(attempts - 1, 0, 10))));

    /// <summary>ينفذ MaxTransientAttempts وفق قواعد AiGovernor الحالية دون تغيير عقد المستدعين.</summary>
    public static int MaxTransientAttempts(IConfiguration config) =>
        Math.Clamp(config.GetValue<int?>("Ai:MaxTransientAttempts") ?? 8, 1, 50);

    /// <summary>ينفذ PalestineDayStart وفق قواعد AiGovernor الحالية دون تغيير عقد المستدعين.</summary>
    public static DateTimeOffset PalestineDayStart(DateTimeOffset now) =>
        DateEvidenceRules.AtPalestineMidnight(SourceSchedule.LocalDate(now));

    /// <summary>ينفذ NextPalestineDay وفق قواعد AiGovernor الحالية دون تغيير عقد المستدعين.</summary>
    public static DateTimeOffset NextPalestineDay(DateTimeOffset now) =>
        DateEvidenceRules.AtPalestineMidnight(SourceSchedule.LocalDate(now).AddDays(1));

    /// <summary>ينفذ DailyTokenBudget وفق قواعد AiGovernor الحالية دون تغيير عقد المستدعين.</summary>
    public static long DailyTokenBudget(IConfiguration config) =>
        config.GetValue<long?>("Ai:DailyTokenBudget") ?? DefaultDailyTokenBudget;

    // Counts the Palestinian calendar day; a non-positive budget disables the governor.
    /// <summary>يجمع استهلاك اليوم الفلسطيني ويقارنه بميزانية الرموز المعتمدة.</summary>
    public static async Task<bool> BudgetExceededAsync(
        HiringDb db,
        IConfiguration config,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var budget = DailyTokenBudget(config);
        if (budget <= 0)
            return false;
        var start = PalestineDayStart(now);
        var used =
            await db
                .AiUsage.Where(x => x.StartedAt >= start)
                .SumAsync(x => (long?)x.TotalTokens, ct)
            ?? 0;
        return used >= budget;
    }
}
