namespace HiringPs.Core;

/// <summary>Internal preparation result; the candidate remains inside the extraction transaction boundary.</summary>
internal sealed record PreparedExtractionCandidate(
    Opportunity Candidate,
    CheckedDates Dates,
    List<string> Errors
);
