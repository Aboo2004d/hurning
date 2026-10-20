namespace HiringPs.Core;

/// <summary>تفاصيل قابلة للعرض بعد فحص الاستحقاق؛ روابط المصادر قيم مستقلة دون رسم كيانات قاعدة البيانات.</summary>
public sealed record OpportunityDetailsDto : OpportunityCardDto
{
    /// <summary>مستوى الخبرة المعروض في التفاصيل.</summary>
    public string ExperienceLevel { get; init; } = "";
    /// <summary>مدة التدريب المنشورة.</summary>
    public string Duration { get; init; } = "";
    /// <summary>رسوم التدريب المنشورة إن وجدت.</summary>
    public string Cost { get; init; } = "";
    /// <summary>شروط الأهلية المنشورة.</summary>
    public string Eligibility { get; init; } = "";
    /// <summary>رابط التقديم أو الإعلان الأصلي عند غياب رابط مصدر.</summary>
    public string ApplyUrl { get; init; } = "";
    /// <summary>آخر وقت تحقق؛ ليس تاريخ النشر الأصلي.</summary>
    public DateTimeOffset LastVerifiedAt
    {
        get; init;
    }
    /// <summary>مفتاح الجهة لاختيار رابط تصفحها دون إعادة استنتاجه من الاسم.</summary>
    public string NormalizedOrganization { get; init; } = "";
    /// <summary>المفتاح المكاني لاختيار روابط المدن المطابقة.</summary>
    public string LocationSearchText { get; init; } = "";
    public List<OpportunitySourceSummaryDto> Sources { get; init; } = [];
    public List<string> Attachments { get; init; } = [];
}
