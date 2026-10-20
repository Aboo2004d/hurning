namespace HiringPs.Core;

/// <summary>صف في محفوظات العضو؛ لا يحتوي كيان الفرصة أو هوية عضو آخر.</summary>
public sealed record SavedOpportunitySummaryDto
{
    public long Id
    {
        get; init;
    }
    public long OpportunityId
    {
        get; init;
    }
    public DateTimeOffset SavedAt
    {
        get; init;
    }
    public OpportunityKind Kind
    {
        get; init;
    }
    public string Title { get; init; } = "";
    public string Organization { get; init; } = "";
    public string Locations { get; init; } = "";
    public PublicationStatus Status
    {
        get; init;
    }
}
