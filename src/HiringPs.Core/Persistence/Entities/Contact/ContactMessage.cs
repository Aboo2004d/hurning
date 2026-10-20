using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>رسالة تواصل واردة مع حالة قراءتها ومعالجتها من الإدارة.</summary>
public class ContactMessage
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }

    /// <summary>الاسم المعروض للمستخدم أو الجهة المرتبطة.</summary>
    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    /// <summary>البريد الإلكتروني لصاحب الرسالة أو الطلب.</summary>
    [Required, MaxLength(254)]
    public string Email { get; set; } = "";

    /// <summary>نص الرسالة أو البلاغ المرسل للموقع.</summary>
    [Required, MaxLength(5000)]
    public string Message { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>يشير إلى قراءة الإدارة للرسالة.</summary>
    public bool IsRead { get; set; }
    /// <summary>يشير إلى معالجة الإدارة للرسالة.</summary>
    public bool IsHandled { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
