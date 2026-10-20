using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>مؤشرات دفعات مهام العضوية وحجز تشغيلها كي تستأنف بعد توقف العامل.</summary>
public sealed class MembershipRuntimeState
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    [Key]
    public int Id { get; set; } = 1;
    /// <summary>نهاية حجز المعالجة لمنع تنافس أكثر من عامل على السجل.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
    /// <summary>مؤشر آخر اشتراك عولج ضمن دفعة التذكيرات.</summary>
    public long ReminderAfterSubscription { get; set; }
    /// <summary>مؤشر آخر فرصة محفوظة عولجت ضمن دفعة التذكيرات.</summary>
    public long ReminderAfterSaved { get; set; }
}
