namespace HiringPs.Core;

/// <summary>نسخة تاريخية من محتوى عنصر مصدر قبل تغييره.</summary>
public class SourceItemRevision
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي لعنصر المصدر المرتبط.</summary>
    public long SourceItemId { get; set; }
    /// <summary>بصمة المحتوى لاكتشاف التغييرات وتجنب المعالجة المكررة.</summary>
    public string ContentHash { get; set; } = "";
    /// <summary>المحتوى الخام المحفوظ قبل الاستخراج.</summary>
    public string Content { get; set; } = "";
    /// <summary>وقت حفظ نسخة المحتوى.</summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
