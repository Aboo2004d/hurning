using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class ResetPasswordInput
{
    [
        Required(ErrorMessage = "FieldRequired"),
        MinLength(8, ErrorMessage = "PasswordMinLength"),
        DataType(DataType.Password)
    ]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "PasswordMismatch"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
