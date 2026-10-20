using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>تحدي تحقق محدود الصلاحية وغرض الاستخدام؛ لا يحفظ الرموز مكشوفة.</summary>
public sealed class MemberChallenge
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";
    /// <summary>الغرض من تحدي التحقق؛ لا يستخدم التحدي لغرض آخر.</summary>
    public ChallengePurpose Purpose { get; set; }

    /// <summary>بصمة رابط التحقق المستخدمة لمطابقة الطلب دون حفظ السر الأصلي.</summary>
    [MaxLength(64)]
    public string LinkHash { get; set; } = "";

    /// <summary>بصمة رمز التحقق؛ لا تخزن قيمة الرمز الأصلية.</summary>
    [MaxLength(1000)]
    public string CodeHash { get; set; } = "";

    /// <summary>رقم الهاتف المتوقع عند إكمال التحقق.</summary>
    [MaxLength(32)]
    public string ExpectedMobile { get; set; } = "";
    /// <summary>معرّف مستخدم Telegram الذي تم التحقق من ملكيته.</summary>
    public long? TelegramUserId { get; set; }
    /// <summary>معرّف محادثة Telegram الخاصة بتحدي التحقق.</summary>
    public long? ChatId { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت انتهاء صلاحية السجل أو التفويض؛ لا يقبل الاستخدام بعده.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>وقت إرسال رمز التحقق.</summary>
    public DateTimeOffset? CodeSentAt { get; set; }
    /// <summary>وقت استهلاك رابط التحقق.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
    /// <summary>وقت اكتمال العملية المرتبطة بالسجل.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>عدد محاولات التنفيذ أو التحقق المسجلة.</summary>
    public int Attempts { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
