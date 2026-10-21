using System.ComponentModel.DataAnnotations;

namespace HiringPs.Web.Models;

public sealed class TelegramComposeInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OpportunityId { get; set; }
    public long? TemplateVersionId { get; set; }
    [Required, MaxLength(4096)]
    public string Text { get; set; } = "";
    public string[] Targets { get; set; } = [];
}
