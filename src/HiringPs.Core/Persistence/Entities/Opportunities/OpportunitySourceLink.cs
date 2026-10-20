using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>دليل يربط فرصة بعنصر مصدر وفرصة فرعية داخله دون تكرار الرابط.</summary>
public class OpportunitySourceLink
{
    /// <summary>المعرّف الأساسي للسجل؛ يختلف نوعه وطريقة توليده حسب الكيان.</summary>
    public long Id { get; set; }
    /// <summary>المفتاح الخارجي للفرصة المرتبطة بالسجل.</summary>
    public long OpportunityId { get; set; }
    /// <summary>الفرصة المرتبطة التي يحمّلها EF Core عند الحاجة.</summary>
    public Opportunity Opportunity { get; set; } = null!;
    /// <summary>المفتاح الخارجي لعنصر المصدر المرتبط.</summary>
    public long SourceItemId { get; set; }
    /// <summary>عنصر المصدر الذي يمثل دليل الفرصة.</summary>
    public SourceItem SourceItem { get; set; } = null!;

    /// <summary>مفتاح الفرصة داخل عنصر مصدر قد يحتوي عدة فرص.</summary>
    [MaxLength(100)]
    public string ChildKey { get; set; } = "";
}
