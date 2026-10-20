namespace HiringPs.Core;

/// <summary>Delivery administration fields without Telegram destination or locking metadata.</summary>
public sealed record MemberMessageSummaryDto(
    long Id,
    string UserId,
    string Text,
    DateTimeOffset? SentAt,
    DateTimeOffset? CancelledAt,
    string LastError,
    int Attempts
);
