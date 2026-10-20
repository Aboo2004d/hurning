using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>مؤشرات نسخ تعليمات AI المعتمدة والمقترحة لمسار الاستخراج.</summary>
public class WorkflowSettings
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key]
    public int Id { get; set; } = 1;
    /// <summary>نسخة تعليمات AI المعتمدة حاليًا للمعالجة.</summary>
    public int ActivePromptVersionId { get; set; } = 1;
    /// <summary>نسخة تعليمات AI التي تنتظر المراجعة والاعتماد.</summary>
    public int? PendingPromptVersionId { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
