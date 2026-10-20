namespace HiringPs.Core;

/// <summary>قاعدة إتاحة تفاصيل نوع فرصة للعرض الإداري دون كيان قاعدة البيانات.</summary>
public sealed record OpportunityAccessSummaryDto(OpportunityKind Kind, bool PublicDetails);
