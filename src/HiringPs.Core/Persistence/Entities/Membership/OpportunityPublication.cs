using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

// A publication marker is written in the same transaction as publication.
/// <summary>حدث نشر أولي يستأنف منه العامل توزيع إشعارات الفرصة على الأعضاء.</summary>
public sealed class OpportunityPublication
{
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;

    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    [Key]
    public long OpportunityId { get; set; }
    /// <summary>وقت نشر الفرصة في الموقع.</summary>
    public DateTimeOffset PublishedAt { get; set; }
    /// <summary>مؤشر العضو الأخير المعالج عند توزيع إشعارات النشر على دفعات.</summary>
    public string? AfterUserId { get; set; }
    /// <summary>يشير إلى اكتمال معالجة جميع عناصر العملية.</summary>
    public bool Completed { get; set; }
}
