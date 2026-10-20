namespace HiringPs.Core;

/// <summary>Latest selected worker heartbeat as a read-only value.</summary>
public sealed record WorkerHeartbeatSummaryDto(
    DateTimeOffset At,
    string State
);
