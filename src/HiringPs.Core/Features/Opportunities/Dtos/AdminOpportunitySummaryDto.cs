namespace HiringPs.Core;

/// <summary>صف قائمة الفرص مع قرار أهلية النشر المحسوب من حالة الفرصة وموعدها ودليل الإغلاق.</summary>
public sealed record AdminOpportunitySummaryDto(long Id, string Title, OpportunityKind Kind,
    string Organization, PublicationStatus Status, string ReviewReason, bool CanPublish);
