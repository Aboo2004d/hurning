using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>نقطة استئناف اكتشاف مرتبطة بمصدر موجود ولا تولد هوية مستقلة.</summary>
public class DiscoveryCheckpoint
{
    /// <summary>المفتاح الخارجي للمصدر الذي ينتمي إليه السجل.</summary>
    [Key]
    public int SourceId { get; set; }

    /// <summary>بصمة إعدادات الاكتشاف التي تنتمي إليها نقطة الاستئناف.</summary>
    [MaxLength(64)]
    public string ConfigurationHash { get; set; } = "";

    /// <summary>رابط صفحة الاكتشاف التالية لاستئناف الجمع.</summary>
    [MaxLength(1500)]
    public string NextUrl { get; set; } = "";
    /// <summary>عدد صفحات المصدر التي اكتملت معالجتها.</summary>
    public int CompletedPages { get; set; }
    /// <summary>عدد محاولات تكرار طلب الجمع المسجلة.</summary>
    public int ReplayClicks { get; set; }
    /// <summary>يشير إلى اكتمال معالجة جميع عناصر العملية.</summary>
    public bool Completed { get; set; }
    /// <summary>عدد العناصر التي اكتشفتها جولة الجمع.</summary>
    public int Found { get; set; }
    /// <summary>عناوين الصفحات المعالجة بصيغة JSON لمنع الدوران.</summary>
    public string VisitedUrlsJson { get; set; } = "[]";
    /// <summary>وقت آخر تحديث للسجل.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
