using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>نموذج محرر Opportunity يفصل HTTP عن قاعدة البيانات.</summary>
public sealed class OpportunityInput
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
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string[] AvailableCategories { get; set; } = [];
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

    /// <summary>ينقل حقول النموذج إلى عقد الخدمة فقط.</summary>
    public OpportunityEditDto ToDto() => new()
    {
            Id = Id,
            Kind = Kind,
            Title = Title,
            Organization = Organization,
            OriginalLanguage = OriginalLanguage,
            Summary = Summary,
            Locations = Locations,
            Category = Category,
            EmploymentType = EmploymentType,
            ExperienceLevel = ExperienceLevel,
            WorkMode = WorkMode,
            ReferenceNumber = ReferenceNumber,
            Duration = Duration,
            Eligibility = Eligibility,
            Cost = Cost,
            ApplyUrl = ApplyUrl,
            Deadline = Deadline,
            Status = Status,
            ClosureEvidence = ClosureEvidence,
            Version = Version,
            ReviewReason = ReviewReason
    };

    /// <summary>يجهز النموذج من بيانات القراءة المستقلة.</summary>
    public static OpportunityInput From(OpportunityEditDto value) => new()
    {
            Id = value.Id,
            Kind = value.Kind,
            Title = value.Title,
            Organization = value.Organization,
            OriginalLanguage = value.OriginalLanguage,
            Summary = value.Summary,
            Locations = value.Locations,
            Category = value.Category,
            EmploymentType = value.EmploymentType,
            ExperienceLevel = value.ExperienceLevel,
            WorkMode = value.WorkMode,
            ReferenceNumber = value.ReferenceNumber,
            Duration = value.Duration,
            Eligibility = value.Eligibility,
            Cost = value.Cost,
            ApplyUrl = value.ApplyUrl,
            Deadline = value.Deadline,
            Status = value.Status,
            ClosureEvidence = value.ClosureEvidence,
            Version = value.Version,
            ReviewReason = value.ReviewReason
    };
}
