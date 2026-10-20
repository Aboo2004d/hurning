using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>أوامر إدارة نشر القنوات؛ تحافظ على تأكيد إعادة المحاولة ومبدأ عدم تكرار إرسال مجهول.</summary>
public sealed class TelegramChannelManagementService(HiringDb db, TelegramChannelPublisher telegram)
{
    /// <summary>يعدل تفعيل القناة ووقت بدء النشر دون رجوع تلقائي للمنشورات القديمة.</summary>
    public async Task<TelegramChannelActionResultDto> ToggleAsync(string target, bool enabled, string actor, CancellationToken ct)
    {
        if (!TelegramChannelTargets.IsTarget(target)) return new(TelegramChannelActionOutcome.InvalidRequest);
        var setting = await db.TelegramChannelSettings.FindAsync([target], ct);
        if (setting is null) { setting = new TelegramChannelSetting { Target = target }; db.TelegramChannelSettings.Add(setting); }
        if (setting.Enabled != enabled)
        {
            setting.Enabled = enabled;
            // Only opportunities first published after enabling are posted automatically.
            setting.EnabledAt = enabled ? DateTimeOffset.UtcNow : setting.EnabledAt;
            setting.Actor = actor;
            db.Audit.Add(new AuditEntry { Actor = setting.Actor, Action = "TelegramChannel.Enabled", Detail = target + ":" + enabled });
            await db.SaveChangesAsync(ct);
        }
        return new(TelegramChannelActionOutcome.Completed, "SavedChanges");
    }

    /// <summary>يرسل رسالة اختبار واحدة بطلب الإدارة ويسجل نتيجتها دون إعادة إرسال غامضة.</summary>
    public async Task<TelegramChannelActionResultDto> TestAsync(string target, string actor, CancellationToken ct)
    {
        if (!TelegramChannelTargets.IsTarget(target)) return new(TelegramChannelActionOutcome.InvalidRequest);
        TelegramChannelResult result;
        try
        {
            // A disconnected browser must not cancel recording the outcome.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            result = await telegram.TestAsync(target, budget.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message == "PublicOriginRequired")
        {
            result = new(TelegramChannelOutcome.Rejected, ex.Message);
        }
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "TelegramChannel.Test",
            Detail = target + ":" + (result.Outcome == TelegramChannelOutcome.Sent ? "Sent" : result.Code) });
        await db.SaveChangesAsync(CancellationToken.None);
        return new(TelegramChannelActionOutcome.Completed, result.Outcome == TelegramChannelOutcome.Sent ? "TelegramChannelTestSent" : result.Code);
    }

    /// <summary>يعيد جدولة سجل غير منشور؛ النتيجة المجهولة تتطلب تأكيدًا صريحًا.</summary>
    public async Task<TelegramChannelActionResultDto> RetryAsync(Guid id, bool confirmUnknown, string actor, CancellationToken ct)
    {
        var post = await db.SocialPublications.SingleOrDefaultAsync(x => x.Id == id && x.Provider == TelegramChannelTargets.Provider, ct);
        if (post is null) return new(TelegramChannelActionOutcome.NotFound);
        if (post.Status is "Published" or "Pending") return new(TelegramChannelActionOutcome.Completed);
        // An unknown result may already be in the channel: retry only after explicit confirmation.
        if (post.Status == "Unknown" && !confirmUnknown) { return new(TelegramChannelActionOutcome.Completed, "TelegramRetryConfirm"); }
        post.Status = "Pending";
        post.Attempts = 0;
        post.NextAttemptAt = DateTimeOffset.UtcNow;
        post.Error = "";
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "TelegramChannel.Retry", Detail = id.ToString() });
        await db.SaveChangesAsync(ct);
        return new(TelegramChannelActionOutcome.Completed, "SavedChanges");
    }
}
