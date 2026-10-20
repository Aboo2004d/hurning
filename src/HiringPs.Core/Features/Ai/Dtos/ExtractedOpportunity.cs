// طلب Gemini وقراءة ردّه: حدود المدخلات والمخرجات، مخطط JSON، وتصنيف أخطاء المزوّد.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون ExtractedOpportunity ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public class ExtractedOpportunity
{
    public string ChildKey { get; set; } = "";
    public OpportunityKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
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
    public string ApplyUrl { get; set; } = "";
    public DateOnly? Deadline { get; set; }
    public string DeadlineEvidence { get; set; } = "";
    public DateOnly? PublicationDate { get; set; }
    public string PublicationEvidence { get; set; } = "";
    public DateOnly? ModificationDate { get; set; }
    public string ModificationEvidence { get; set; } = "";
    public bool DeadlineIsExtension { get; set; }
    public List<ExtractedDate> OtherDates { get; set; } = [];
    public string LocationEvidence { get; set; } = "";
    public bool? IsInPalestine { get; set; }
    public string ReviewReason { get; set; } = "";
}
