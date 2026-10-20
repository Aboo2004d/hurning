using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>طلب مراجعة دفع يقدمه العضو وتبت فيه الإدارة.</summary>
public sealed class PaymentReview
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>معرّف طلب الدفع المرتبط بالسجل.</summary>
    public Guid OrderId { get; set; }

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";

    /// <summary>ملاحظة صاحب طلب المراجعة.</summary>
    [MaxLength(2000)]
    public string Note { get; set; } = "";

    /// <summary>اسم المرفق المرتبط بالمراجعة، إن وجد.</summary>
    [MaxLength(100)]
    public string? AttachmentName { get; set; }
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت إنهاء المراجعة.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>تفاصيل القرار الذي أنهى المراجعة.</summary>
    [MaxLength(2000)]
    public string Resolution { get; set; } = "";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
