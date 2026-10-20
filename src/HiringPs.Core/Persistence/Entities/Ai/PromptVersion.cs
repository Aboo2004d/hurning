using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>نسخة تعليمات AI محفوظة للتتبع ولا تعدل بعد استخدامها.</summary>
public class PromptVersion
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }
    /// <summary>نص تعليمات الاستخراج في نسخة الـprompt.</summary>
    public string Template { get; set; } = "";
    /// <summary>نسخة البيانات القديمة المحفوظة للتوافق أو الاسترجاع.</summary>
    public string LegacySnapshot { get; set; } = "";
    /// <summary>النص النهائي المعتمد بعد المعالجة أو المراجعة.</summary>
    public string FinalText { get; set; } = "";
    /// <summary>قواعد تفسير التواريخ ضمن نسخة تعليمات AI.</summary>
    public string DateContract { get; set; } = PromptDatePolicy.CurrentDateContract;

    /// <summary>حد الملخص المعتمد في نسخة تعليمات الاستخراج.</summary>
    [Range(40, 2000)]
    public int SummaryLimit { get; set; } = 200;

    /// <summary>النطاق الجغرافي الذي تغطيه الفرصة.</summary>
    [MaxLength(500)]
    public string GeographicScope { get; set; } = "West Bank, Gaza and Jerusalem";

    /// <summary>أسماء أنواع الفرص المسموح باستخراجها، مفصولة بفواصل داخل تعليمات AI.</summary>
    [MaxLength(200)]
    public string AllowedKinds { get; set; } = "Job,Training,Tender,ProcurementNotice";
    /// <summary>يسمح بقبول فرصة بلا موعد نهائي مثبت وفق سياسة المصدر.</summary>
    public bool AllowUnknownDeadline { get; set; } = true;

    /// <summary>ملاحظة صاحب طلب المراجعة.</summary>
    [MaxLength(500)]
    public string Note { get; set; } = "";

    /// <summary>هوية منشئ النسخة.</summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Render() => FinalText.Length > 0 ? FinalText : Compose();

    /// <summary>يركب تعليمات الاستخراج النهائية من القالب وسياسة التواريخ والشروط المعتمدة.</summary>
    public string Compose() =>
        Template
            .Replace(
                "{{summaryLimit}}",
                SummaryLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
            )
            .Replace("{{scope}}", GeographicScope)
            .Replace("{{allowedKinds}}", AllowedKinds)
            .Replace("{{allowUnknownDeadline}}", AllowUnknownDeadline ? "true" : "false")
        + $"\nACTIVE SYSTEM CONDITIONS: Allowed opportunity kinds: {AllowedKinds}. Geographic scope within Palestine: {GeographicScope}. Summary target: at most {SummaryLimit} characters; this is an AI writing instruction, not a database length restriction. "
        + (
            AllowUnknownDeadline
                ? "Missing closing date alone never requires review. "
                : "An unspecified closing date requires review. "
        )
        + DateContract;
}
