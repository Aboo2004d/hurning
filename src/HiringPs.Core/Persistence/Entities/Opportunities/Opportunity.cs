using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>فرصة منشورة أو قيد المراجعة مع بياناتها وأدلة مصادرها.</summary>
public class Opportunity
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>نوع الفرصة أو الفحص أو الرسالة وفق enum الخاص بالسجل.</summary>
    public OpportunityKind Kind { get; set; }

    /// <summary>عنوان الفرصة المعروض بلغته الأصلية.</summary>
    [Required]
    public string Title { get; set; } = "";

    /// <summary>اسم الجهة الناشرة للفرصة.</summary>
    [Required]
    public string Organization { get; set; } = "";

    /// <summary>اسم الجهة بعد التطبيع لاكتشاف التطابق والتكرار.</summary>
    [MaxLength(200)]
    public string NormalizedOrganization { get; set; } = "";

    /// <summary>لغة المحتوى الأصلي؛ تستخدم لاختيار العرض ورابط الفرصة.</summary>
    [MaxLength(30)]
    public string OriginalLanguage { get; set; } = "ar";
    /// <summary>ملخص الفرصة باللغة الأصلية.</summary>
    public string Summary { get; set; } = "";
    /// <summary>وصف مواقع الفرصة كما يعرض للمستخدم.</summary>
    public string Locations { get; set; } = "";
    /// <summary>تصنيف الفرصة.</summary>
    public string Category { get; set; } = "";
    /// <summary>نوع عقد العمل كما استُخلص من الإعلان.</summary>
    public string EmploymentType { get; set; } = "";
    /// <summary>مستوى الخبرة المطلوب.</summary>
    public string ExperienceLevel { get; set; } = "";
    /// <summary>نمط العمل: حضوري أو عن بعد أو غيره وفق النص المحفوظ.</summary>
    public string WorkMode { get; set; } = "";
    /// <summary>رقم المرجع المنشور للفرصة.</summary>
    public string ReferenceNumber { get; set; } = "";
    /// <summary>مدة جولة الجمع.</summary>
    public string Duration { get; set; } = "";
    /// <summary>شروط أهلية التقديم كما وردت في الإعلان.</summary>
    public string Eligibility { get; set; } = "";
    /// <summary>التكلفة المحسوبة أو المسجلة للاستدعاء وفق المنطق الحالي.</summary>
    public string Cost { get; set; } = "";

    /// <summary>رابط التقديم الخارجي.</summary>
    [MaxLength(1500)]
    public string ApplyUrl { get; set; } = "";
    /// <summary>عناوين المرفقات المكتشفة بصيغة JSON.</summary>
    public string AttachmentsJson { get; set; } = "[]";
    /// <summary>آخر موعد للتقديم عند وجود دليل موثوق عليه.</summary>
    public DateOnly? Deadline { get; set; }
    /// <summary>تاريخ النشر الأصلي كما ورد في المصدر.</summary>
    public DateTimeOffset? SourcePublishedAt { get; set; }
    /// <summary>تاريخ آخر تعديل كما ورد في المصدر.</summary>
    public DateTimeOffset? SourceModifiedAt { get; set; }
    /// <summary>معرّف نسخة تعليمات AI المستخدمة في هذه العملية.</summary>
    public int? PromptVersionId { get; set; }
    /// <summary>النص الموحد المفهرس للبحث في الفرصة.</summary>
    public string SearchText { get; set; } = "";
    /// <summary>النص المفهرس للبحث بالمواقع بعد التطبيع.</summary>
    public string LocationSearchText { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت آخر تحقق من صلاحية معلومات الفرصة.</summary>
    public DateTimeOffset LastVerifiedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    public PublicationStatus Status { get; set; } = PublicationStatus.NeedsReview;
    /// <summary>يحمي تعديلات الإدارة من الاستبدال أثناء إعادة الاستخراج.</summary>
    public bool AdminOverride { get; set; }

    /// <summary>بصمة الفرصة المستخدمة للمساعدة في اكتشاف التكرار.</summary>
    [MaxLength(64)]
    public string Fingerprint { get; set; } = "";
    /// <summary>سبب إحالة المحتوى للمراجعة البشرية.</summary>
    public string ReviewReason { get; set; } = "";

    /// <summary>الدليل على إغلاق الفرصة؛ يمنع إعادة نشر فرصة مغلقة.</summary>
    [MaxLength(1000)]
    public string ClosureEvidence { get; set; } = "";
    /// <summary>روابط الأدلة التي تربط الفرصة بعناصر المصادر.</summary>
    public ICollection<OpportunitySourceLink> SourceLinks { get; set; } = [];

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
