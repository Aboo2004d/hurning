using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>سياسة إتاحة تفاصيل نوع من الفرص للزوار قبل احتساب صلاحيات الاشتراك.</summary>
public sealed class OpportunityAccessRule
{
    /// <summary>نوع الفرصة أو الفحص أو الرسالة وفق enum الخاص بالسجل.</summary>
    [Key]
    public OpportunityKind Kind { get; set; }
    /// <summary>يسمح للزوار بقراءة التفاصيل الكاملة لهذا النوع.</summary>
    public bool PublicDetails { get; set; } = true;
}
