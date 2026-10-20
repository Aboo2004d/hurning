using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>نتيجة فحص آمنة مرتبطة بطلب وعامل محددين.</summary>
public class ToolCheckResult
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>معرّف طلب الفحص الذي أنتج النتيجة.</summary>
    public long RequestId { get; set; }

    /// <summary>المفتاح المعتمد للأداة التي جرى فحصها.</summary>
    [MaxLength(40)]
    public string Tool { get; set; } = "";
    /// <summary>الحالة الحالية للعملية أو نتيجة الفحص؛ القيم معرفة في enum الخاص بالكيان.</summary>
    public ToolState State { get; set; }

    /// <summary>الكود المعتمد للخصم أو نتيجة الفحص بحسب نوع السجل.</summary>
    [MaxLength(80)]
    public string Code { get; set; } = "";

    /// <summary>تفاصيل آمنة لنتيجة الفحص دون أسرار أو محتوى مستندات خاص.</summary>
    [MaxLength(1000)]
    public string Detail { get; set; } = "";

    /// <summary>معرّف نسخة العامل التي نفذت الطلب أو الفحص.</summary>
    [MaxLength(32)]
    public string WorkerId { get; set; } = "";
    /// <summary>وقت تنفيذ الفحص الذي أنتج النتيجة.</summary>
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>مدة تنفيذ الفحص بالمللي ثانية.</summary>
    public long DurationMs { get; set; }
}
