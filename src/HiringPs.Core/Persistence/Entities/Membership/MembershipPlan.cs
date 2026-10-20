using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>هوية الباقة وترتيب ظهورها ورابط نسختها الحالية؛ الشروط المالية في MembershipPlanVersion.</summary>
public sealed class MembershipPlan
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public int Id { get; set; }
    /// <summary>يتحكم في ظهور الباقة ضمن قائمة المبيعات.</summary>
    public bool Visible { get; set; } = true;
    /// <summary>ترتيب العرض التصاعدي.</summary>
    public int SortOrder { get; set; }
    /// <summary>معرّف نسخة الباقة المعتمدة للمشتريات الجديدة.</summary>
    public int? CurrentVersionId { get; set; }

    /// <summary>رمز rowversion الذي يمنع الكتابة فوق تعديلات متزامنة.</summary>
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
