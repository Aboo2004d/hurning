using System.ComponentModel.DataAnnotations;
namespace HiringPs.Web.Models;
public sealed class TelegramMemberInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(3500)] public string Body { get; set; } = "";
    public bool PaidOnly { get; set; } = true;
    [MaxLength(450)] public string? UserId { get; set; }
}
