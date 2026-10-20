// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل SourceItem وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class SourceItem
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي للمصدر الذي ينتمي إليه السجل.</summary>
    public int SourceId { get; set; }
    /// <summary>المصدر المرتبط بالسجل.</summary>
    public Source Source { get; set; } = null!;

    /// <summary>معرّف العنصر لدى المصدر؛ يميز العنصر مع SourceId.</summary>
    [MaxLength(200)]
    public string ExternalId { get; set; } = "";

    /// <summary>عنوان المورد الخارجي أو المنشور المرتبط بالسجل.</summary>
    [MaxLength(1500)]
    public string Url { get; set; } = "";
    /// <summary>المحتوى الخام المحفوظ قبل الاستخراج.</summary>
    public string Content { get; set; } = "";

    /// <summary>بصمة المحتوى لاكتشاف التغييرات وتجنب المعالجة المكررة.</summary>
    [MaxLength(64)]
    public string ContentHash { get; set; } = "";
    /// <summary>حالة السجل ضمن دورة العمل؛ يستخدم النوع الحالي كما هو محفوظ في القاعدة.</summary>
    public ProcessingStatus Status { get; set; }
    /// <summary>وقت أول اكتشاف للمحتوى لدى الموقع.</summary>
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت آخر مشاهدة للمحتوى في المصدر.</summary>
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    // Last successful fetch of the original page; published items are re-verified after an interval.
    /// <summary>وقت آخر تحقق ناجح من المحتوى؛ يحدد أهلية إعادة جلب العنصر.</summary>
    public DateTimeOffset? LastVerifiedAt { get; set; }
    /// <summary>أقرب وقت يسمح فيه بإعادة معالجة العنصر.</summary>
    public DateTimeOffset? RetryAt { get; set; }
    /// <summary>عدد محاولات التنفيذ أو التحقق المسجلة.</summary>
    public int Attempts { get; set; }

    /// <summary>ملخص خطأ الجولة وفق قيود السجلات الحالية.</summary>
    [MaxLength(2000)]
    public string Error { get; set; } = "";
    /// <summary>نتيجة استخراج AI المحفوظة بصيغتها المنظمة.</summary>
    public string ExtractionJson { get; set; } = "";
    /// <summary>نسخة تعليمات AI التي أنتجت بيانات الاستخراج المحفوظة.</summary>
    public int? ExtractionPromptVersionId { get; set; }
    /// <summary>نسخة تعليمات AI المطلوبة لهذه العملية.</summary>
    public int? RequestedPromptVersionId { get; set; }
    /// <summary>عناوين المرفقات المكتشفة بصيغة JSON.</summary>
    public string AttachmentsJson { get; set; } = "[]";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
