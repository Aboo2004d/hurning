using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>محاولة نشر مستقلة لكل وجهة، تحفظ النتيجة وتمنع الإرسال المتكرر غير المقصود.</summary>
public sealed class SocialPublication
{
    /// <summary>معرّف نسخة قالب النشر التي استُخدمت لإنشاء النص.</summary>
    public long? TemplateVersionId { get; set; }
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public Guid Id { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>وجهة الربط أو النشر؛ يميز المؤسسة عن الحساب الشخصي.</summary>
    [MaxLength(20)]
    public string Provider { get; set; } = "";
    /// <summary>معرّف الصفحة أو الحساب لدى مزود النشر.</summary>
    [MaxLength(100)]
    public string PageId { get; set; } = "";
    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";
    /// <summary>النص المراد إرساله أو الذي أرسل فعليًا.</summary>
    public string Text { get; set; } = "";
    /// <summary>معرّف المنشور الذي أعاده مزود النشر.</summary>
    [MaxLength(500)]
    public string RemoteId { get; set; } = "";
    /// <summary>عنوان المورد الخارجي أو المنشور المرتبط بالسجل.</summary>
    [MaxLength(1500)]
    public string Url { get; set; } = "";
    /// <summary>هوية المستخدم أو العامل الذي نفذ العملية.</summary>
    [MaxLength(256)]
    public string Actor { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
