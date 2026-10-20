using System.ComponentModel.DataAnnotations;

namespace HiringPs.Web.Models;

/// <summary>حقول أمر النشر المسموح استقبالها من المتصفح دون بيانات السجل أو الربط أو القالب المعروض.</summary>
public sealed class SocialPublishInput
{
    public Guid Id { get; set; }
    public long OpportunityId { get; set; }
    public string Provider { get; set; } = "";
    public string[]? Targets { get; set; }
    public long? TemplateVersionId { get; set; }
    [Required, MaxLength(50000)]
    public string Text { get; set; } = "";
    public bool ConfirmRepeat { get; set; }
}
