using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class VerificationInput
{
    [Required(ErrorMessage = "FieldRequired"), RegularExpression("^[0-9]{6}$")]
    public string Code { get; set; } = "";
    public string? BotLink { get; set; }
    public string? NewMobile { get; set; }
}
