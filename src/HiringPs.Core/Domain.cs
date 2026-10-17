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
    public PublicationStatus Status { get; set; } = PublicationStatus.NeedsReview;
    public bool AdminOverride { get; set; }

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

    public static DateOnly PalestineToday()
    {
        // Try known IANA and Windows time zone identifiers, then fall back to any matching zone.
        string[] candidates = new[] { "Asia/Hebron", "Israel Standard Time", "Palestine Standard Time", "Jerusalem", "Middle East Standard Time" };
        TimeZoneInfo? tz = null;
        foreach (var id in candidates)
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                if (tz != null) break;
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        if (tz == null)
        {
            tz = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(z =>
                    z.Id.Contains("Jerusalem", System.StringComparison.OrdinalIgnoreCase) ||
                    z.Id.Contains("Israel", System.StringComparison.OrdinalIgnoreCase) ||
                    z.DisplayName.Contains("Jerusalem", System.StringComparison.OrdinalIgnoreCase) ||
                    z.DisplayName.Contains("Israel", System.StringComparison.OrdinalIgnoreCase)
                );
        }

        if (tz == null) tz = TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
