namespace HiringPs.Core;

/// <summary>Pending update summary; excludes the inbound JSON payload.</summary>
public sealed record TelegramInboxSummaryDto(
    long Id,
    DateTimeOffset ReceivedAt,
    int Attempts
);
