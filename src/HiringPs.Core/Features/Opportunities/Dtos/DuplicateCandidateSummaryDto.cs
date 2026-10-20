namespace HiringPs.Core;

/// <summary>بيانات زوج التكرار المعروضة للإدارة دون كيانات EF.</summary>
public sealed record DuplicateCandidateSummaryDto(long Id, long OpportunityId, long CandidateId,
    DuplicateOpportunitySummaryDto Opportunity, DuplicateOpportunitySummaryDto Candidate,
    string Reason, DuplicateCandidateStatus Status, long? MergeOperationId);
