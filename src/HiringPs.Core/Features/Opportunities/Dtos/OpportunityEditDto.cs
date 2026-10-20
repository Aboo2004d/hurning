using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>حقول محرر Opportunity المسموحة دون كيان EF أو علاقات التنقل.</summary>
public sealed class OpportunityEditDto
{
    public long Id { get; set; }
    public OpportunityKind Kind { get; set; }
    [Required]
    public string Title { get; set; } = "";
    [Required]
    public string Organization { get; set; } = "";
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
    public DateOnly? Deadline { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.NeedsReview;
    [MaxLength(1000)]
    public string ClosureEvidence { get; set; } = "";
    public byte[] Version { get; set; } = [];
    public string ReviewReason { get; set; } = "";
}
