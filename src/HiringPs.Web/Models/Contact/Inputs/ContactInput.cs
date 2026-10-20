using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class ContactInput
{
    [
        Required(ErrorMessage = "ContactNameRequired"),
        MaxLength(120, ErrorMessage = "ContactNameLength")
    ]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "ContactEmailRequired"),
        EmailAddress(ErrorMessage = "ContactEmailInvalid"),
        MaxLength(254, ErrorMessage = "ContactEmailInvalid")
    ]
    public string Email { get; set; } = "";

    [
        Required(ErrorMessage = "ContactMessageRequired"),
        MaxLength(5000, ErrorMessage = "ContactMessageLength")
    ]
    public string Message { get; set; } = "";
    public string? Website { get; set; }
}
