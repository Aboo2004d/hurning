using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>بلاغ مستخدم عن فرصة مع سبب البلاغ ونتيجة معالجته.</summary>
public class UserReport
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;

    /// <summary>سبب البلاغ أو قرار المعالجة.</summary>
    [Required, MaxLength(60)]
    public string Reason { get; set; } = "";

    /// <summary>التفاصيل المحفوظة للعملية أو البلاغ.</summary>
    [MaxLength(1000)]
    public string Details { get; set; } = "";
    /// <summary>وقت إنشاء السجل بالتوقيت العالمي.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>يشير إلى إنهاء معالجة البلاغ.</summary>
    public bool Resolved { get; set; }
}
