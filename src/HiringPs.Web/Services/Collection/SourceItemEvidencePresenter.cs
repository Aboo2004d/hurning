using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Models;

namespace HiringPs.Web.Services.Collection;

/// <summary>Prepares date evidence for Razor without exposing parsing or PDF lookup logic to the view.</summary>
public static class SourceItemEvidencePresenter
{
    /// <summary>Returns no date section for absent or malformed historical JSON, preserving the existing display behavior.</summary>
    public static IReadOnlyList<ProposedOpportunityDatesDto>? Read(SourceItemDetailsDto item)
    {
        if (string.IsNullOrWhiteSpace(item.ExtractionJson))
            return null;
        ExtractionResult? result;
        try
        {
            result = JsonSerializer.Deserialize<ExtractionResult>(item.ExtractionJson, AiExtractor.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        return result?.Opportunities?.Select(opportunity =>
        {
            var dates = new List<ExtractedDate>();
            if (opportunity.PublicationDate is { } publication)
                dates.Add(new()
                {
                    Kind = OpportunityDateKind.Publication,
                    Date = publication,
                    Evidence = opportunity.PublicationEvidence
                });
            if (opportunity.ModificationDate is { } modification)
                dates.Add(new()
                {
                    Kind = OpportunityDateKind.Modification,
                    Date = modification,
                    Evidence = opportunity.ModificationEvidence
                });
            if (opportunity.Deadline is { } deadline)
                dates.Add(new()
                {
                    Kind = OpportunityDateKind.Deadline,
                    Date = deadline,
                    Evidence = opportunity.DeadlineEvidence
                });
            dates.AddRange(opportunity.OtherDates ?? []);
            return new ProposedOpportunityDatesDto(opportunity.Title, dates.Where(x => x is not null)
                .Select(date => new ProposedDateEvidenceDto(date.Kind, date.Date, date.Label, date.Evidence,
                    PdfTextFiles.FindPage(item.Content, date.Evidence)?.Page)).ToList());
        }).ToList();
    }
}
