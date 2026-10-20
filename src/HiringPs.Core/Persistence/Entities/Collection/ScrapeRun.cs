using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>ملخص جولة جمع: المصدر والوقت والعدادات ونتيجة التنفيذ.</summary>
public class ScrapeRun
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي للمصدر الذي ينتمي إليه السجل.</summary>
    public int? SourceId { get; set; }
    /// <summary>وقت بدء تنفيذ العملية.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت انتهاء تنفيذ العملية.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    [MaxLength(40)]
    public string Status { get; set; } = "Running";
    /// <summary>عدد العناصر التي اكتشفتها جولة الجمع.</summary>
    public int Found { get; set; }
    /// <summary>عدد العناصر التي تغيرت خلال الجولة.</summary>
    public int Changed { get; set; }

    /// <summary>ملخص خطأ الجولة وفق قيود السجلات الحالية.</summary>
    [MaxLength(2000)]
    public string Error { get; set; } = "";
}
