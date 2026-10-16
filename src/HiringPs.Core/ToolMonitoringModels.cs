using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

public enum ToolCheckKind
{
    Standard,
    AiLive,
}

public enum ToolRequestStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}

public enum ToolState
{
    Ready,
    Problem,
    NotConfigured,
    Configured,
    Disabled,
    Unknown,
}

public class ToolCheckRequest
{
    public long Id { get; set; }
    public ToolCheckKind Kind { get; set; }
    public ToolRequestStatus Status { get; set; }
    public int? ActiveSlot { get; set; }

    [MaxLength(200)]
    public string Actor { get; set; } = "";

    [MaxLength(32)]
    public string WorkerId { get; set; } = "";

    [MaxLength(80)]
    public string Outcome { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public class ToolCheckResult
{
    public long Id { get; set; }
    public long RequestId { get; set; }

    [MaxLength(40)]
    public string Tool { get; set; } = "";
    public ToolState State { get; set; }

    [MaxLength(80)]
    public string Code { get; set; } = "";

    [MaxLength(1000)]
    public string Detail { get; set; } = "";

    [MaxLength(32)]
    public string WorkerId { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public long DurationMs { get; set; }
}

public class ToolWorkerState
{
    [Key, MaxLength(32)]
    public string Id { get; set; } = "";

    [MaxLength(200)]
    public string Host { get; set; } = "";

    [MaxLength(200)]
    public string Account { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset At { get; set; }
    public bool Collecting { get; set; }
    public DateTimeOffset? LastLoopAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }

    [MaxLength(100)]
    public string ErrorType { get; set; } = "";
}

public record ToolProbe(string Tool, ToolState State, string Code, string Detail = "");

public record CollectionRuntimeSnapshot(
    bool Collecting,
    DateTimeOffset? LastLoopAt = null,
    DateTimeOffset? LastSuccessAt = null,
    DateTimeOffset? LastFailureAt = null,
    string ErrorType = ""
);

// A separate heartbeat stays fresh even when discovery or OCR occupies the collection loop.
public sealed class ToolWorkerRuntime
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    private CollectionRuntimeSnapshot snapshot = new(false);
    public CollectionRuntimeSnapshot Snapshot => Volatile.Read(ref snapshot);

    public void Begin() =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = true,
                LastLoopAt = DateTimeOffset.UtcNow,
            }
        );

    public void Succeed() =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = false,
                LastSuccessAt = DateTimeOffset.UtcNow,
                ErrorType = "",
            }
        );

    public void Fail(Exception error) =>
        Volatile.Write(
            ref snapshot,
            Snapshot with
            {
                Collecting = false,
                LastFailureAt = DateTimeOffset.UtcNow,
                ErrorType = error.GetType().Name.Truncate(100),
            }
        );
}

public static class ToolMonitoringRules
{
    public static readonly string[] StandardTools =
    [
        "AiConfiguration",
        "Chromium",
        "PdfText",
        "PdfRender",
        "PdfOcr",
        "PdfLanguages",
        "PdfWorkspace",
        "PdfPipeline",
    ];

    public static bool WorkerFresh(ToolWorkerState? worker, DateTimeOffset now) =>
        worker is not null && worker.At >= now.AddSeconds(-60) && worker.At <= now.AddSeconds(30);

    public static bool ResultFresh(
        ToolCheckResult result,
        ToolWorkerState? worker,
        DateTimeOffset now
    ) =>
        WorkerFresh(worker, now)
        && result.WorkerId == worker!.Id
        && result.CheckedAt >= now.AddHours(-2)
        && result.CheckedAt <= now.AddSeconds(30);
}
