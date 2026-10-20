using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class ResetInput
{
    [
        Required(ErrorMessage = "FieldRequired"),
        RegularExpression("^[1-9][0-9]{7,14}$", ErrorMessage = "MobileInternational")
    ]
    public string Mobile { get; set; } = "";
}
