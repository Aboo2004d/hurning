using HiringPs.Core;

namespace HiringPs.Web.Models;
/// <summary>A quoted extracted date with its optional PDF page reference, prepared for display.</summary>
public sealed record ProposedDateEvidenceDto(
    OpportunityDateKind Kind,
    DateOnly Date,
    string Label,
    string Evidence,
    int? PdfPage
);
