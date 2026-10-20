using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>رابطة حفظ بين عضو وفرصة؛ تستخدم لقائمة المحفوظات وتذكيرات الموعد.</summary>
public sealed class SavedOpportunity
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }

    /// <summary>معرّف مستخدم Identity المرتبط بالسجل.</summary>
    [MaxLength(450)]
    public string UserId { get; set; } = "";
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>وقت حفظ العضو للفرصة.</summary>
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
