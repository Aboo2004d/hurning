namespace HiringPs.Core;

/// <summary>Administration report fields without the opportunity entity graph.</summary>
public sealed record UserReportSummaryDto(
    long Id,
    long OpportunityId,
    string OpportunityTitle,
    string Reason,
    string Details,
    bool Resolved
);
