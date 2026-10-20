using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>حقول محرر Source المسموحة دون كيان EF أو علاقات التنقل.</summary>
public sealed class SourceEditDto
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
    public byte[] Version { get; set; } = [];
}
