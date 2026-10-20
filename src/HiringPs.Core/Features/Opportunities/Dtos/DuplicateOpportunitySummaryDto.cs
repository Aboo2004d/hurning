namespace HiringPs.Core;

/// <summary>حقول مقارنة فرصة مرشحة للتكرار دون نصوصها أو علاقاتها الكاملة.</summary>
public sealed record DuplicateOpportunitySummaryDto(long Id, string Title, string Organization,
    PublicationStatus Status, DateOnly? Deadline);
