// نماذج قاعدة البيانات والحالات الأساسية للمصادر والإعلانات ونتائج المعالجة.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace HiringPs.Core;

public enum OpportunityKind
{
    Job,
    Training,
    Tender,
    ProcurementNotice,
}

public enum PublicationStatus
{
    Draft,
    NeedsReview,
    Published,
    Archived,
    Hidden,
}

public enum ProcessingStatus
{
    Discovered,
    AwaitingAi,
    Retry,
    Ready,
    NeedsReview,
    OutOfScope,
    Published,
}

public enum ConnectorKind
{
    Gpc,
    PalCareer,
    BrowserSelectors,
    ReliefWeb,
    Automatic,
    UnrwaInspira,
    JobsRss,
    ReliefWebRss,
}

public class Source
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Url { get; set; } = "";
    public ConnectorKind Connector { get; set; }
    public bool Enabled { get; set; }
    public bool UsageApproved { get; set; }

    [MaxLength(2000)]
    public string ApprovalNote { get; set; } = "";
    public string ConfigurationJson { get; set; } = "{}";
    public int IntervalHours { get; set; } = 24;
    public TimeOnly CollectionStartTime { get; set; } = TimeOnly.MinValue;
    public DateOnly? ScheduleAnchorDate { get; set; }
    public DateTimeOffset? CollectionRetryAt { get; set; }
    public ICollection<SourceCollectionRequest> CollectionRequests { get; set; } = [];
    public DateTimeOffset NextDueAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSuccessAt { get; set; }

    [MaxLength(2000)]
    public string LastError { get; set; } = "";

    // Reset by a successful discovery; drives exponential retry backoff.
    public int ConsecutiveFailures { get; set; }

    // Collector health note for administrators (repeated failures or possible template change).
    [MaxLength(500)]
    public string HealthWarning { get; set; } = "";
    public DateTimeOffset? HealthWarningAt { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public class SourceCollectionRequest
{
    [
        Key,
        System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(
            System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None
        )
    ]
    public int SourceId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(SourceId))]
    [System.ComponentModel.DataAnnotations.Schema.InverseProperty(
        nameof(HiringPs.Core.Source.CollectionRequests)
    )]
    public Source Source { get; set; } = null!;
    public Guid RequestToken { get; set; } = Guid.NewGuid();
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool DiscoveryFinished { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = "Pending";

    [MaxLength(1000)]
    public string Error { get; set; } = "";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public class SourceItem
{
    public long Id { get; set; }
    public int SourceId { get; set; }
    public Source Source { get; set; } = null!;

    [MaxLength(200)]
    public string ExternalId { get; set; } = "";

    [MaxLength(1500)]
    public string Url { get; set; } = "";
    public string Content { get; set; } = "";

    [MaxLength(64)]
    public string ContentHash { get; set; } = "";
    public ProcessingStatus Status { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    // Last successful fetch of the original page; published items are re-verified after an interval.
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public DateTimeOffset? RetryAt { get; set; }
    public int Attempts { get; set; }

    [MaxLength(2000)]
    public string Error { get; set; } = "";
    public string ExtractionJson { get; set; } = "";
    public int? ExtractionPromptVersionId { get; set; }
    public int? RequestedPromptVersionId { get; set; }
    public string AttachmentsJson { get; set; } = "[]";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public class Opportunity
{
    public long Id { get; set; }
    public OpportunityKind Kind { get; set; }

    [Required]
    public string Title { get; set; } = "";

    [Required]
    public string Organization { get; set; } = "";

    [MaxLength(200)]
    public string NormalizedOrganization { get; set; } = "";

    [MaxLength(30)]
    public string OriginalLanguage { get; set; } = "ar";
    public string Summary { get; set; } = "";
    public string Locations { get; set; } = "";
    public string Category { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string WorkMode { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Eligibility { get; set; } = "";
    public string Cost { get; set; } = "";

    [MaxLength(1500)]
    public string ApplyUrl { get; set; } = "";
    public string AttachmentsJson { get; set; } = "[]";
    public DateOnly? Deadline { get; set; }
    public DateTimeOffset? SourcePublishedAt { get; set; }
    public DateTimeOffset? SourceModifiedAt { get; set; }
    public int? PromptVersionId { get; set; }
    public string SearchText { get; set; } = "";
    public string LocationSearchText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastVerifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Persisted COALESCE(SourcePublishedAt, CreatedAt) used for public ordering; computed by SQL Server.</summary>
    public DateTimeOffset SortDate { get; private set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.NeedsReview;
    public bool AdminOverride { get; set; }

    // Other records were merged into this one; extraction keeps its text but still applies verified dates.
    public bool IsMergeTarget { get; set; }

    [MaxLength(200)]
    public string NormalizedReference { get; set; } = "";

    [MaxLength(64)]
    public string Fingerprint { get; set; } = "";
    public string ReviewReason { get; set; } = "";

    [MaxLength(1000)]
    public string ClosureEvidence { get; set; } = "";
    public ICollection<OpportunitySourceLink> SourceLinks { get; set; } = [];

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public class OpportunitySourceLink
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    public long SourceItemId { get; set; }
    public SourceItem SourceItem { get; set; } = null!;

    [MaxLength(100)]
    public string ChildKey { get; set; } = "";
}

public class SourceItemRevision
{
    public long Id { get; set; }
    public long SourceItemId { get; set; }
    public string ContentHash { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ScrapeRun
{
    public long Id { get; set; }
    public int? SourceId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = "Running";
    public int Found { get; set; }
    public int Changed { get; set; }

    [MaxLength(2000)]
    public string Error { get; set; } = "";
}

public class UserReport
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Reason { get; set; } = "";

    [MaxLength(1000)]
    public string Details { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Resolved { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }

    [MaxLength(200)]
    public string Actor { get; set; } = "";

    [MaxLength(200)]
    public string Action { get; set; } = "";

    [MaxLength(2000)]
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class WorkerHeartbeat
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string State { get; set; } = "";
}

// Single global row (Id = 1) for collector decisions shared across ticks and worker restarts.
public class PipelineState
{
    [Key]
    public int Id { get; set; } = 1;
    public DateTimeOffset? AiPausedUntil { get; set; }

    [MaxLength(300)]
    public string AiPauseReason { get; set; } = "";
    public int? LastRequeuedPromptVersionId { get; set; }
    public DateTimeOffset? LastRequeuedAt { get; set; }
}

// Maps a normalised organisation spelling to its canonical normalised key for duplicate matching.
public class OrganizationAlias
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Alias { get; set; } = "";

    [Required, MaxLength(200)]
    public string Canonical { get; set; } = "";

    [MaxLength(256)]
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum DuplicateCandidateStatus
{
    Open,
    Merged,
    Dismissed,
}

// A near-match found during extraction; administrators merge or dismiss it.
public class DuplicateCandidate
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    public long CandidateId { get; set; }
    public Opportunity Candidate { get; set; } = null!;
    public double Score { get; set; }

    [MaxLength(300)]
    public string Reason { get; set; } = "";
    public DuplicateCandidateStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    [MaxLength(256)]
    public string ResolvedBy { get; set; } = "";
    public long? MergeOperationId { get; set; }
}

public static class TextRules
{
    public static int CharacterCount(string text) => new StringInfo(text).LengthInTextElements;

    public static string Hash(string text) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))
        );

    public static string OrganizationKey(string text)
    {
        var value = Normalize(text);
        return value.Length <= 200 ? value : "sha256:" + Hash(value);
    }

    public static string Normalize(string text) =>
        string.Join(
            ' ',
            text.ToLowerInvariant()
                .Normalize()
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );

    private static readonly Lazy<TimeZoneInfo> PalestineZone = new(ResolvePalestineTimeZone);

    public static TimeZoneInfo PalestineTimeZone => PalestineZone.Value;

    public static DateOnly PalestineToday() =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, PalestineTimeZone).DateTime
        );

    // Set when the server lacks a Palestinian zone; the worker logs it once.
    public static string? PalestineTimeZoneWarning { get; private set; }

    private static TimeZoneInfo ResolvePalestineTimeZone()
    {
        // Palestinian zones first (Windows id, then IANA); Israeli zones differ around DST changes.
        string[] palestinian = ["West Bank Standard Time", "Asia/Hebron", "Asia/Gaza"];
        string[] fallbacks =
        [
            "Israel Standard Time",
            "Palestine Standard Time",
            "Jerusalem",
            "Middle East Standard Time",
        ];
        foreach (var id in palestinian.Concat(fallbacks))
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                if (!palestinian.Contains(id))
                    Fallback($"Palestinian time zone not found; using {id}.");
                return zone;
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var match = TimeZoneInfo
            .GetSystemTimeZones()
            .FirstOrDefault(zone =>
                zone.Id.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                || zone.Id.Contains("Israel", StringComparison.OrdinalIgnoreCase)
                || zone.DisplayName.Contains("Jerusalem", StringComparison.OrdinalIgnoreCase)
                || zone.DisplayName.Contains("Israel", StringComparison.OrdinalIgnoreCase)
            );
        Fallback(
            match is null
                ? "No Palestinian time zone found; using UTC."
                : $"Palestinian time zone not found; using {match.Id}."
        );
        return match ?? TimeZoneInfo.Utc;
    }

    private static void Fallback(string warning)
    {
        PalestineTimeZoneWarning = warning;
        System.Diagnostics.Trace.TraceWarning(warning);
    }
}
