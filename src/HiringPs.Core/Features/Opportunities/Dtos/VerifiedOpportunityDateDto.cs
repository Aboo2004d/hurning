namespace HiringPs.Core;

/// <summary>تاريخ مثبت واقتباس دليله للعرض؛ لا يحتوي مفاتيح مصدر أو رموز تزامن.</summary>
public sealed record VerifiedOpportunityDateDto(
    OpportunityDateKind Kind, DateOnly Date, string Label, string Evidence, bool IsExtension
);
