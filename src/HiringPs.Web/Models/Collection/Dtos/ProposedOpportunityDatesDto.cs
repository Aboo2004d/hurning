namespace HiringPs.Web.Models;
/// <summary>Extracted title and its proposed dates for the administrator's evidence review.</summary>
public sealed record ProposedOpportunityDatesDto(
    string Title,
    IReadOnlyList<ProposedDateEvidenceDto> Dates
);
