using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class RegisterInput
{
    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "FieldRequired"),
        EmailAddress(ErrorMessage = "EmailInvalid"),
        StringLength(256)
    ]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "FieldRequired"), StringLength(32)]
    public string Mobile { get; set; } = "";

    [
        Required(ErrorMessage = "FieldRequired"),
        MinLength(8, ErrorMessage = "PasswordMinLength"),
        DataType(DataType.Password)
    ]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "PasswordMismatch"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
