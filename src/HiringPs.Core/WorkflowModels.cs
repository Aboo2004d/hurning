using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HiringPs.Core;

public class PromptVersion
{
    public int Id { get; set; }
    public string Template { get; set; } = "";
    public string LegacySnapshot { get; set; } = "";
    public string FinalText { get; set; } = "";
    public string DateContract { get; set; } = PromptDefaults.DateInstructions;

    [Range(40, 2000)]
    public int SummaryLimit { get; set; } = 200;

    [MaxLength(500)]
    public string GeographicScope { get; set; } = "West Bank, Gaza and Jerusalem";

    [MaxLength(200)]
    public string AllowedKinds { get; set; } = "Job,Training,Tender,ProcurementNotice";
    public bool AllowUnknownDeadline { get; set; } = true;

    [MaxLength(500)]
    public string Note { get; set; } = "";

    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Render() => FinalText.Length > 0 ? FinalText : Compose();

    public string Compose() =>
        Template
            .Replace(
                "{{summaryLimit}}",
                SummaryLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
            )
            .Replace("{{scope}}", GeographicScope)
            .Replace("{{allowedKinds}}", AllowedKinds)
            .Replace("{{allowUnknownDeadline}}", AllowUnknownDeadline ? "true" : "false")
        + $"\nACTIVE SYSTEM CONDITIONS: Allowed opportunity kinds: {AllowedKinds}. Geographic scope within Palestine: {GeographicScope}. Summary target: at most {SummaryLimit} characters; this is an AI writing instruction, not a database length restriction. "
        + (
            AllowUnknownDeadline
                ? "Missing closing date alone never requires review. "
                : "An unspecified closing date requires review. "
        )
        + DateContract;
}

public class WorkflowSettings
{
    [Key]
    public int Id { get; set; } = 1;
    public int ActivePromptVersionId { get; set; } = 1;
    public int? PendingPromptVersionId { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public record AiExecution(PromptVersion Version, long? SourceItemId);

public class AiUsage
{
    public long Id { get; set; }
    public int PromptVersionId { get; set; }
    public long? SourceItemId { get; set; }

    [MaxLength(100)]
    public string Model { get; set; } = "";

    [MaxLength(64)]
    public string PromptHash { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public long DurationMs { get; set; }
    public int? HttpStatus { get; set; }
    public bool? Success { get; set; }

    [MaxLength(100)]
    public string ErrorType { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CachedTokens { get; set; }
    public int? TotalTokens { get; set; }
}

public class AiTelemetry
{
    public int? HttpStatus { get; set; }
    public int? Input { get; set; }
    public int? Output { get; set; }
    public int? Cached { get; set; }
    public int? Total { get; set; }

    public void Read(JsonElement response)
    {
        if (
            !response.TryGetProperty("usageMetadata", out var usage)
            || usage.ValueKind != JsonValueKind.Object
        )
            return;
        int? ReadCount(string key) =>
            usage.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var count)
            && count >= 0
                ? count
                : null;
        Input = ReadCount("promptTokenCount");
        Output = ReadCount("candidatesTokenCount");
        Cached = ReadCount("cachedContentTokenCount");
        Total = ReadCount("totalTokenCount");
    }
}

public class DiscoveryCheckpoint
{
    [Key]
    public int SourceId { get; set; }

    [MaxLength(64)]
    public string ConfigurationHash { get; set; } = "";

    [MaxLength(1500)]
    public string NextUrl { get; set; } = "";
    public int CompletedPages { get; set; }
    public int ReplayClicks { get; set; }
    public bool Completed { get; set; }
    public int Found { get; set; }
    public string VisitedUrlsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public record DiscoveryBatch(
    IReadOnlyList<(string Id, string Url)> Items,
    string? NextUrl,
    int CompletedPages,
    int ReplayClicks = 0
);

public enum OpportunityDateKind
{
    Publication,
    Modification,
    Deadline,
    BookletPurchase,
    SiteVisit,
    BidOpening,
    Other,
}

public class ExtractedDate
{
    public OpportunityDateKind Kind { get; set; }
    public DateOnly Date { get; set; }
    public string Label { get; set; } = "";
    public string Evidence { get; set; } = "";
}

public class OpportunityDateRecord
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    public long SourceItemId { get; set; }
    public int? PromptVersionId { get; set; }
    public OpportunityDateKind Kind { get; set; }
    public DateOnly Date { get; set; }

    [MaxLength(300)]
    public string Label { get; set; } = "";
    public string Evidence { get; set; } = "";

    [MaxLength(64)]
    public string EvidenceHash { get; set; } = "";
    public bool IsExtension { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Opportunity Opportunity { get; set; } = null!;
}

public class MergeOperation
{
    public long Id { get; set; }
    public long DuplicateId { get; set; }
    public long TargetId { get; set; }
    public PublicationStatus OriginalStatus { get; set; }
    public bool DuplicateOverride { get; set; }
    public bool TargetOverride { get; set; }
    public string OriginalReviewReason { get; set; } = "";
    public string LinkIdsJson { get; set; } = "[]";
    public byte[] DuplicateVersion { get; set; } = [];
    public byte[] DuplicateBeforeVersion { get; set; } = [];
    public byte[] TargetBeforeVersion { get; set; } = [];
    public byte[] TargetVersion { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UndoneAt { get; set; }

    [MaxLength(256)]
    public string Actor { get; set; } = "";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public class RetentionPolicy
{
    [Key]
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; } = true;

    [Range(7, 3650)]
    public int RevisionDays { get; set; } = 90;

    [Range(7, 3650)]
    public int DiagnosticDays { get; set; } = 30;
    public DateTimeOffset? LastCleanupAt { get; set; }
    public int LastDeletedRevisions { get; set; }
    public int LastDeletedDiagnostics { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}
