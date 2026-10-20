using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة صلاحية وبيانات حساب عضو واحد دون إرجاع ملف EF إلى الواجهة.</summary>
public sealed class MemberAccountReadService(HiringDb db)
{
    /// <summary>يعيد حالة الحساب لفحص التفعيل والإيقاف قبل تنفيذ إجراءات منطقة العضو.</summary>
    public Task<MemberAccountSummaryDto?> ReadAsync(string owner, CancellationToken ct) =>
        db.MemberProfiles.AsNoTracking().Where(x => x.UserId == owner).Select(x => new MemberAccountSummaryDto
        {
            UserId = x.UserId,
            Name = x.Name,
            Mobile = x.Mobile,
            Activated = x.Activated,
            Suspended = x.Suspended,
            SuspensionReason = x.SuspensionReason,
            TelegramLinked = x.TelegramChatId.HasValue,
            NotificationsEnabled = x.NotificationsEnabled,
            SavedRemindersEnabled = x.SavedRemindersEnabled,
        }).SingleOrDefaultAsync(ct);

    /// <summary>يقرأ أحدث خطأ تسليم غير مرسل وغير ملغى للحساب نفسه.</summary>
    public async Task<string> DeliveryErrorAsync(string owner, CancellationToken ct) =>
        await db.MemberMessages.AsNoTracking().Where(x => x.UserId == owner && x.SentAt == null
            && x.CancelledAt == null && x.LastError != "").OrderByDescending(x => x.Id)
            .Select(x => x.LastError).FirstOrDefaultAsync(ct) ?? "";
}
