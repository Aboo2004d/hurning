namespace HiringPs.Core;

/// <summary>Validated retention update with its original rowversion token.</summary>
public sealed record RetentionUpdateDto(
    bool Enabled,
    int RevisionDays,
    int DiagnosticDays,
    byte[] Version
);
