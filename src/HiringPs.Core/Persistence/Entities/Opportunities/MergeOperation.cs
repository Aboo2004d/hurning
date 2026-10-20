using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>أثر دمج فرصتين مع البيانات اللازمة للتتبع والتراجع المتوافق مع التزامن.</summary>
public class MergeOperation
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>معرّف الفرصة المكررة التي دُمجت.</summary>
    public long DuplicateId { get; set; }
    /// <summary>معرّف الفرصة الأساسية التي استقبلت الدمج.</summary>
    public long TargetId { get; set; }
    /// <summary>حالة الفرصة قبل الدمج.</summary>
    public PublicationStatus OriginalStatus { get; set; }
    /// <summary>حالة حماية تعديلات الإدارة على الفرصة المكررة قبل الدمج.</summary>
    public bool DuplicateOverride { get; set; }
    /// <summary>حالة حماية تعديلات الإدارة على الفرصة الأساسية قبل الدمج.</summary>
    public bool TargetOverride { get; set; }
    /// <summary>سبب المراجعة قبل الدمج.</summary>
    public string OriginalReviewReason { get; set; } = "";
    /// <summary>معرّفات روابط المصادر المتأثرة بالدمج بصيغة JSON.</summary>
    public string LinkIdsJson { get; set; } = "[]";
    /// <summary>نسخة التزامن للفرصة المكررة وقت الدمج.</summary>
    public byte[] DuplicateVersion { get; set; } = [];
    /// <summary>قيمة rowversion للفرصة المكررة قبل الدمج للتحقق من تسلسل التراجع.</summary>
    public byte[] DuplicateBeforeVersion { get; set; } = [];
    /// <summary>قيمة rowversion للفرصة الأساسية قبل الدمج للتحقق من تسلسل التراجع.</summary>
    public byte[] TargetBeforeVersion { get; set; } = [];
    /// <summary>نسخة التزامن للفرصة الأساسية وقت الدمج.</summary>
    public byte[] TargetVersion { get; set; } = [];
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>وقت التراجع عن عملية الدمج إن حدث.</summary>
    public DateTimeOffset? UndoneAt { get; set; }

    /// <summary>هوية المستخدم أو العامل الذي نفذ العملية.</summary>
    [MaxLength(256)]
    public string Actor { get; set; } = "";

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
