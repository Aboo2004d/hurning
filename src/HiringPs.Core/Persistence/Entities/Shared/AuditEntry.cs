using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>سجل تدقيق للعملية ومن نفذها والتفاصيل المسموح بتخزينها.</summary>
public class AuditEntry
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id
    {
        get; set;
    }

    /// <summary>هوية المستخدم أو العامل الذي نفذ العملية.</summary>
    [MaxLength(200)]
    public string Actor { get; set; } = "";

    /// <summary>اسم العملية المسجلة في سجل التدقيق.</summary>
    [MaxLength(200)]
    public string Action { get; set; } = "";

    /// <summary>تفاصيل آمنة لنتيجة الفحص دون أسرار أو محتوى مستندات خاص.</summary>
    [MaxLength(2000)]
    public string Detail { get; set; } = "";
    /// <summary>وقت وقوع الحدث بالتوقيت العالمي؛ يحول إلى توقيت فلسطين عند العرض.</summary>
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
