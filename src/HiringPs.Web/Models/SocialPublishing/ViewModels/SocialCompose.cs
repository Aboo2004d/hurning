using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Areas.Admin.Controllers;

/// <summary>بيانات عرض تجهيز منشور؛ لا يحمل كيانات الربط أو رموز وصول المزودين.</summary>
public sealed class SocialCompose
{
    public long? TemplateVersionId { get; set; }
    public int TemplateNumber { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OpportunityId { get; set; }
    public string Provider { get; set; } = "";
    public string[]? Targets { get; set; }
    public List<SocialDestinationSummaryDto> Destinations { get; set; } = [];
    public Dictionary<string, string> PublishResults { get; set; } = [];
    public SocialOpportunitySummaryDto? Opportunity { get; set; }
    [Required, MaxLength(50000)] public string Text { get; set; } = "";
    public string Link { get; set; } = "";
    public bool PreviouslyPublished { get; set; }
    public bool ConfirmRepeat { get; set; }
    public List<SocialPublicationSummaryDto> History { get; set; } = [];
}
