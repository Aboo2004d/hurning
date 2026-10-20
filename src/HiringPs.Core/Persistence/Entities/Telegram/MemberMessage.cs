using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>رسالة عضو مؤجلة مع مفاتيح منع التكرار وحجز التنفيذ.</summary>
public sealed class MemberMessage
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";

    /// <summary>مفتاح فريد للحدث يمنع إنشاء رسالة مكررة للعضو.</summary>
    [MaxLength(200)]
    public string EventKey { get; set; } = "";
    /// <summary>نوع الفرصة أو الفحص أو الرسالة وفق enum الخاص بالسجل.</summary>
    public MemberMessageKind Kind { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long? OpportunityId { get; set; }
    /// <summary>وقت الحدث الذي أنتج الرسالة.</summary>
    public DateTimeOffset? EventAt { get; set; }

    /// <summary>النص المراد إرساله أو الذي أرسل فعليًا.</summary>
    [MaxLength(4000)]
    public string Text { get; set; } = "";
    /// <summary>أقرب وقت يسمح فيه بمعالجة الرسالة.</summary>
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت نجاح إرسال الرسالة.</summary>
    public DateTimeOffset? SentAt { get; set; }
    /// <summary>وقت إلغاء الرسالة قبل إرسالها.</summary>
    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>نهاية حجز المعالجة لمنع تنافس أكثر من عامل على السجل.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
    /// <summary>عدد محاولات التنفيذ أو التحقق المسجلة.</summary>
    public int Attempts { get; set; }

    /// <summary>رمز آخر خطأ معالجة؛ لا يتضمن مفاتيح أو محتوى مزود حساسًا.</summary>
    [MaxLength(200)]
    public string LastError { get; set; } = "";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
