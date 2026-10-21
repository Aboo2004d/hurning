using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>خطأ تشغيلي مستقل عن سجل العمليات؛ الأوقات مخزنة بصيغة UTC.</summary>
[Index(nameof(At))]
[Index(nameof(TraceId))]
public sealed class ApplicationError
{
    /// <summary>رقم السجل.</summary>
    public long Id { get; set; }
    /// <summary>وقت حدوث الخطأ UTC.</summary>
    public DateTimeOffset At { get; set; }
    /// <summary>الويب أو العامل.</summary>
    [MaxLength(32)] public string Application { get; set; } = "";
    /// <summary>مستوى السجل.</summary>
    [MaxLength(16)] public string Level { get; set; } = "";
    /// <summary>مصدر رسالة الخطأ في الكود.</summary>
    [MaxLength(512)] public string Category { get; set; } = "";
    /// <summary>رقم المتابعة نفسه المعروض للمستخدم.</summary>
    [MaxLength(200)] public string TraceId { get; set; } = "";
    /// <summary>مسار الطلب دون الاستعلام أو محتوى النموذج.</summary>
    [MaxLength(2048)] public string Path { get; set; } = "";
    /// <summary>نوع الاستثناء إن وجد.</summary>
    [MaxLength(512)] public string ExceptionType { get; set; } = "";
    /// <summary>الرسالة بعد حجب القيم السرية المعروفة.</summary>
    [MaxLength(8000)] public string Message { get; set; } = "";
    /// <summary>سلسلة الاستثناءات ومواقع الكود بعد حجب القيم السرية المعروفة.</summary>
    public string Details { get; set; } = "";
}
