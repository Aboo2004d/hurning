namespace HiringPs.Core;

/// <summary>شروط نسخة باقة للعرض؛ لا يغيرها المتصفح ولا تتبعها EF.</summary>
public sealed record MembershipPlanVersionSummaryDto
{
    public int Id { get; init; }
    public int PlanId { get; init; }
    public string NameAr { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescriptionAr { get; init; } = "";
    public string DescriptionEn { get; init; } = "";
    public string BadgeAr { get; init; } = "";
    public string BadgeEn { get; init; } = "";
    public decimal PriceIls { get; init; }
    public int Days { get; init; }
    public int DetailKinds { get; init; }
    public int AlertKinds { get; init; }
    public int? MaxPreferredCategories { get; init; }

    /// <summary>يختار الاسم وفق لغة العرض بنفس سياسة النسخة المخزنة.</summary>
    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
