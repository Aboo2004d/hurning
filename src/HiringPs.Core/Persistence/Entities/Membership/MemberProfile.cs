using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

// A separate profile leaves the existing administrator Identity records untouched.
/// <summary>ملف العضو المرتبط بمستخدم Identity؛ يفصل بيانات العضوية عن حسابات الإدارة.</summary>
public sealed class MemberProfile
{
    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [Key, MaxLength(450)]
    public string UserId { get; set; } = "";

    /// <summary>الاسم المعروض للمستخدم أو الجهة المرتبطة.</summary>
    [MaxLength(160)]
    public string Name { get; set; } = "";

    /// <summary>رقم هاتف العضو بصيغته المعتمدة في الموقع.</summary>
    [MaxLength(32)]
    public string Mobile { get; set; } = "";

    /// <summary>لغة واجهة العضو ورسائله.</summary>
    [MaxLength(2)]
    public string Language { get; set; } = "ar";
    /// <summary>معرّف مستخدم Telegram الذي تم التحقق من ملكيته.</summary>
    public long? TelegramUserId { get; set; }
    /// <summary>معرّف المحادثة المعتمدة لإرسال رسائل العضو.</summary>
    public long? TelegramChatId { get; set; }
    /// <summary>يشير إلى اكتمال تفعيل حساب العضو.</summary>
    public bool Activated { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>موافقة العضو على استقبال تنبيهات الفرص.</summary>
    public bool NotificationsEnabled { get; set; } = true;
    /// <summary>موافقة العضو على تذكيرات الفرص المحفوظة.</summary>
    public bool SavedRemindersEnabled { get; set; } = true;
    /// <summary>بداية استحقاق التنبيهات لتجنب إرسال فرص أقدم من تفعيلها.</summary>
    public DateTimeOffset NotificationsSince { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>يشير إلى إيقاف خدمات العضو إداريًا.</summary>
    public bool Suspended { get; set; }

    /// <summary>السبب المسجل لإيقاف العضو.</summary>
    [MaxLength(2000)]
    public string SuspensionReason { get; set; } = "";
    /// <summary>وقت تنفيذ الإيقاف.</summary>
    public DateTimeOffset? SuspendedAt { get; set; }
    /// <summary>يحدد ما إذا كان الإيقاف يجمد مدة الاشتراك.</summary>
    public bool FreezeSubscription { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
