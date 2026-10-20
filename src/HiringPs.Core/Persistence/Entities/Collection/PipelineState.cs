// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

/// <summary>سجل PipelineState وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public class PipelineState
{
    /// <summary>المعرف الأساسي للسجل.</summary>
    [Key]
    public int Id { get; set; } = 1;
    /// <summary>موعد انتهاء الإيقاف العام لاستدعاءات AI بسبب الحصة أو الميزانية.</summary>
    public DateTimeOffset? AiPausedUntil { get; set; }

    /// <summary>وصف سبب إيقاف استدعاءات AI دون بيانات اعتماد.</summary>
    [MaxLength(300)]
    public string AiPauseReason { get; set; } = "";
    /// <summary>آخر نسخة تعليمات أعيدت جدولة العناصر المؤهلة لها.</summary>
    public int? LastRequeuedPromptVersionId { get; set; }
    /// <summary>وقت إعادة الجدولة الأخيرة لمنع تكرارها في كل دورة.</summary>
    public DateTimeOffset? LastRequeuedAt { get; set; }
}

// Maps a normalised organisation spelling to its canonical normalised key for duplicate matching.
