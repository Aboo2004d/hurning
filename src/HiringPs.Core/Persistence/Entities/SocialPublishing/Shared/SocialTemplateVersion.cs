using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>نسخة قالب نشر تحفظ نصه ونطاقه؛ يسمح بتغيير اعتمادها دون تعديل محتواها.</summary>
public sealed class SocialTemplateVersion
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }

    /// <summary>وجهة الربط أو النشر؛ يميز المؤسسة عن الحساب الشخصي.</summary>
    [MaxLength(20)]
    public string Provider { get; set; } = "";

    // A string scope allows future opportunity kinds without changing the schema.
    /// <summary>نطاق القالب: نوع فرصة محدد أو القالب العام.</summary>
    [MaxLength(60)]
    public string Scope { get; set; } = "general";
    /// <summary>رقم النسخة المتسلسل ضمن المجال.</summary>
    public int Number { get; set; }
    /// <summary>يشير إلى النسخة المعتمدة حاليًا؛ النسخ السابقة محفوظة للتتبع.</summary>
    public bool Active { get; set; }

    /// <summary>النص الكامل المحفوظ لهذه النسخة.</summary>
    [MaxLength(50000)]
    public string Body { get; set; } = "";

    /// <summary>هوية المستخدم أو العامل الذي نفذ العملية.</summary>
    [MaxLength(256)]
    public string Actor { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
