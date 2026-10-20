namespace HiringPs.Core;

/// <summary>Dashboard counts with the original publication, queue and unresolved-report predicates.</summary>
public sealed record AdminDashboardCountsDto(
    int Published,
    int Review,
    int Queued,
    int Reports
);
