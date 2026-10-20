namespace HiringPs.Core;

/// <summary>Global prompt version pointers and concurrency token for administration.</summary>
public sealed record PromptSettingsDto(
    int ActivePromptVersionId,
    int? PendingPromptVersionId,
    byte[] Version
);
