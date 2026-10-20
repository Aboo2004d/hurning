using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>أوامر إعدادات تليجرام للعضو؛ تبقى الكوكيز والتنقل والتحقق من جلسة HTTP في الويب.</summary>
public sealed class MemberNotificationSettingsService(HiringDb db, MemberVerification verification)
{
    /// <summary>يوقف موافقة استقبال التنبيهات دون حذف الربط أو الاشتراك.</summary>
    public async Task StopAsync(string owner, CancellationToken ct)
    {
        var profile = await db.MemberProfiles.SingleAsync(x => x.UserId == owner, ct);
        profile.NotificationsEnabled = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>ينشئ تحقق الحساب البديل بعد فحص عدم استهلاك الطلب السابق ثم يستهلكه؛ null تمنع إعادة استعماله.</summary>
    public async Task<VerificationRequest?> ReplaceAsync(string owner, Guid authorizationId, string mobile, CancellationToken ct)
    {
        var prior = await db.MemberChallenges.SingleAsync(x => x.Id == authorizationId, ct);
        if (prior.CompletedAt is not null)
            return null;
        var request = await verification.CreateAsync(owner, ChallengePurpose.ChangeTelegram, mobile, ct);
        prior.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return request;
    }
}
