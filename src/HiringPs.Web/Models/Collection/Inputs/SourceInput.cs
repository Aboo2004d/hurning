using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>نموذج محرر Source يفصل HTTP عن قاعدة البيانات.</summary>
public sealed class SourceInput
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

    /// <summary>ينقل حقول النموذج إلى عقد الخدمة فقط.</summary>
    public SourceEditDto ToDto() => new()
    {
            Id = Id,
            Name = Name,
            Url = Url,
            Connector = Connector,
            Enabled = Enabled,
            UsageApproved = UsageApproved,
            ApprovalNote = ApprovalNote,
            ConfigurationJson = ConfigurationJson,
            IntervalHours = IntervalHours,
            CollectionStartTime = CollectionStartTime,
            Version = Version
    };

    /// <summary>يجهز النموذج من بيانات القراءة المستقلة.</summary>
    public static SourceInput From(SourceEditDto value) => new()
    {
            Id = value.Id,
            Name = value.Name,
            Url = value.Url,
            Connector = value.Connector,
            Enabled = value.Enabled,
            UsageApproved = value.UsageApproved,
            ApprovalNote = value.ApprovalNote,
            ConfigurationJson = value.ConfigurationJson,
            IntervalHours = value.IntervalHours,
            CollectionStartTime = value.CollectionStartTime,
            Version = value.Version
    };
}
