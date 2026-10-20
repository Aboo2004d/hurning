namespace HiringPs.Core;

/// <summary>Retention editor values and last cleanup metrics without exposing a tracked policy.</summary>
public sealed record RetentionSettingsDto(
    bool Enabled,
    int RevisionDays,
    int DiagnosticDays,
    DateTimeOffset? LastCleanupAt,
    int LastDeletedRevisions,
    int LastDeletedDiagnostics,
    byte[] Version
);
