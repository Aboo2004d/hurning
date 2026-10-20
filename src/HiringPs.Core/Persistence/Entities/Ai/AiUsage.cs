using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>بيانات استهلاك ونتيجة استدعاء AI دون تسجيل مفاتيح الوصول.</summary>
public class AiUsage
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>معرّف نسخة تعليمات AI المستخدمة في هذه العملية.</summary>
    public int PromptVersionId { get; set; }
    /// <summary>المفتاح الخارجي لعنصر المصدر المرتبط.</summary>
    public long? SourceItemId { get; set; }

    /// <summary>اسم نموذج AI المستخدم.</summary>
    [MaxLength(100)]
    public string Model { get; set; } = "";

    /// <summary>بصمة تعليمات AI المستخدمة.</summary>
    [MaxLength(64)]
    public string PromptHash { get; set; } = "";
    /// <summary>وقت بدء تنفيذ العملية.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت انتهاء تنفيذ العملية.</summary>
    public DateTimeOffset? FinishedAt { get; set; }
    /// <summary>مدة تنفيذ الفحص بالمللي ثانية.</summary>
    public long DurationMs { get; set; }
    /// <summary>رمز HTTP الذي أعاده المزود إن توفر.</summary>
    public int? HttpStatus { get; set; }
    /// <summary>نتيجة الاستدعاء؛ قد تبقى فارغة أثناء التنفيذ.</summary>
    public bool? Success { get; set; }

    /// <summary>اسم نوع الخطأ التشخيصي دون نص استثناء قد يتضمن بيانات حساسة.</summary>
    [MaxLength(100)]
    public string ErrorType { get; set; } = "";
    /// <summary>عدد وحدات الإدخال التي أبلغ عنها مزود AI.</summary>
    public int? InputTokens { get; set; }
    /// <summary>عدد وحدات الإخراج التي أبلغ عنها مزود AI.</summary>
    public int? OutputTokens { get; set; }
    /// <summary>عدد وحدات الإدخال المستفادة من التخزين المؤقت لدى المزود.</summary>
    public int? CachedTokens { get; set; }
    /// <summary>إجمالي الوحدات المستهلكة في الاستدعاء.</summary>
    public int? TotalTokens { get; set; }
}
