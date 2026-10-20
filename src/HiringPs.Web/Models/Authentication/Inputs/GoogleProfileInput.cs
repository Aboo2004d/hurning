using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

/// <summary>نموذج إدخال GoogleProfileInput؛ يفصل بيانات واجهة المستخدم عن كيان قاعدة البيانات.</summary>
public sealed class GoogleProfileInput
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "FieldRequired"),
        RegularExpression("^[1-9][0-9]{7,14}$", ErrorMessage = "MobileInternational")
    ]
    public string Mobile { get; set; } = "";
}
