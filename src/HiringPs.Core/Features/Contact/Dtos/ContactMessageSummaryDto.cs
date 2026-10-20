namespace HiringPs.Core;

/// <summary>Inbox fields only; excludes message text and concurrency metadata.</summary>
public sealed record ContactMessageSummaryDto(
    long Id,
    string Name,
    string Email,
    DateTimeOffset CreatedAt,
    bool IsRead,
    bool IsHandled
);
