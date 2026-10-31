using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
namespace HiringPs.Core;

/// <summary>ينفذ مواعيد اليوم الفلسطيني فقط بحجز دائم قبل الاتصال بالمزود.</summary>
public sealed class RandomPublicationScheduler(HiringDb db, IRandomPublicationDispatcher dispatcher, TimeProvider clock)
{
    /// <summary>يعالج الوجهات المفعلة؛ يفوّت المواعيد القديمة بدل إرسال منشورات متراكمة دفعة واحدة.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await RecoverInterruptedAsync(now, ct);
        var local = TimeZoneInfo.ConvertTime(now, TextRules.PalestineTimeZone);
        var day = DateOnly.FromDateTime(local.DateTime);
        var settings = await db.Set<RandomPublicationSetting>().AsNoTracking().Where(x => x.Enabled).ToListAsync(ct);
        foreach (var setting in settings)
        {
            var times = RandomPublicationRules.Times(setting.TimesJson);
            if (!RandomPublicationRules.Valid(setting.DailyCount, setting.MaxRemainingDays, times)) continue;
            for (var index = 0; index < times.Length; index++)
            {
                var due = RandomPublicationRules.Instant(day, times[index]);
                if (due is null || due > now) continue;
                var run = await ReserveAsync(setting.Provider, day, index + 1, due.Value, now, ct);
                if (run is null || run.State != RandomPublicationState.Reserved) continue;
                try
                {
                    var delivery = await dispatcher.SendAsync(run, ct);
                    run.State = delivery.State; run.ResultCode = delivery.Code.Truncate(200);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // The request may have reached the provider. Never replay an uncertain attempt.
                    run.State = RandomPublicationState.Unknown;
                    run.ResultCode = ("Unconfirmed:" + ex.GetType().Name).Truncate(200);
                }
                db.Audit.Add(OperationTracking.Create(OperationArea.Publishing,
                    run.State switch
                    {
                        RandomPublicationState.Published => OperationOutcome.Succeeded,
                        RandomPublicationState.Failed => OperationOutcome.Failed,
                        RandomPublicationState.Skipped => OperationOutcome.Skipped,
                        _ => OperationOutcome.Uncertain,
                    },
                    "RandomPublication", $"{run.Provider}; Slot {run.Slot}; Opportunity {run.OpportunityId}; {run.State}; {run.ResultCode}"));
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>يصالح الحجز المنقطع مع نتيجة الإرسال المحفوظة، دون إعادة أي اتصال خارجي.</summary>
    private async Task RecoverInterruptedAsync(DateTimeOffset now, CancellationToken ct)
    {
        var stale = now.AddMinutes(-5);
        var interrupted = await db.RandomPublicationRuns.Where(x => x.State == RandomPublicationState.Reserved && x.CreatedAt <= stale)
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        foreach (var run in interrupted)
        {
            var status = await db.SocialPublications.AsNoTracking().Where(x => x.Id == run.Id).Select(x => x.Status).SingleOrDefaultAsync(ct);
            run.State = status switch { "Published" => RandomPublicationState.Published, "Failed" => RandomPublicationState.Failed, _ => RandomPublicationState.Unknown };
            run.ResultCode = status == "Published" ? "Published" : "Interrupted";
            db.Audit.Add(OperationTracking.Create(OperationArea.Publishing, OperationOutcome.Uncertain, "RandomPublicationRecovered",
                $"{run.Provider}; Slot {run.Slot}; Opportunity {run.OpportunityId}; {run.State}"));
        }
        if (interrupted.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>يثبت الموعد والوظيفة ذريًا؛ فهارس فريدة تمنع التكرار بين نسخ السيرفر.</summary>
    private async Task<RandomPublicationRun?> ReserveAsync(string provider, DateOnly day, int slot,
        DateTimeOffset due, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Set<RandomPublicationRun>().AnyAsync(x => x.Provider == provider && x.Day == day && x.Slot == slot, ct)) return null;
        var setting = await db.Set<RandomPublicationSetting>().AsNoTracking().SingleAsync(x => x.Provider == provider, ct);
        var times = RandomPublicationRules.Times(setting.TimesJson);
        if (!setting.Enabled || !RandomPublicationRules.Valid(setting.DailyCount, setting.MaxRemainingDays, times)
            || slot > times.Length || RandomPublicationRules.Instant(day, times[slot - 1]) != due) return null;
        var run = new RandomPublicationRun { Provider = provider, Day = day, Slot = slot, ScheduledAt = due, CreatedAt = now };
        if (now - due >= TimeSpan.FromMinutes(5)) { run.State = RandomPublicationState.Skipped; run.ResultCode = "MissedTime"; }
        else
        {
            var recent = now.AddMinutes(-5);
            if (await db.Set<RandomPublicationRun>().AnyAsync(x => x.Provider == provider && x.OpportunityId != null && x.CreatedAt > recent, ct)) return null;
            var connection = await db.SocialConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Provider == provider, ct);
            if (connection is null || !connection.Enabled || connection.ExpiresAt <= now)
            { run.State = RandomPublicationState.Skipped; run.ResultCode = "IntegrationNotConfigured"; }
            else
            {
                var query = RandomPublicationRules.Candidates(db, provider, day, setting.MaxRemainingDays);
                var count = await query.CountAsync(ct);
                if (count == 0) { run.State = RandomPublicationState.Skipped; run.ResultCode = "NoEligibleJobs"; }
                else
                {
                    run.OpportunityId = await query.OrderBy(x => x.Id).Skip(RandomNumberGenerator.GetInt32(count)).Select(x => x.Id).FirstAsync(ct);
                    run.PageId = connection.PageId;
                }
            }
        }
        db.Add(run);
        db.Audit.Add(OperationTracking.Create(OperationArea.Publishing,
            run.State == RandomPublicationState.Reserved ? OperationOutcome.Started : OperationOutcome.Skipped,
            "RandomPublicationSlot", $"{provider}; Slot {slot}; {run.State}; {run.ResultCode}"));
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
        return run;
    }
}
