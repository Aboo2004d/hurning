namespace HiringPs.Core;

/// <summary>Explicit source item details for an authorized administrator; no tracked entity is returned.</summary>
public sealed record SourceItemDetailsDto(
    long Id,
    string SourceName,
    string ExternalId,
    ProcessingStatus Status,
    string Error,
    string Url,
    string Content,
    string ExtractionJson,
    int? ExtractionPromptVersionId
);
