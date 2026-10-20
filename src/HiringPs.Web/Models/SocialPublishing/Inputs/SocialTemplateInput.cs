using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

/// <summary>نموذج إدخال SocialTemplateInput؛ يفصل بيانات واجهة المستخدم عن كيان قاعدة البيانات.</summary>
public sealed class SocialTemplateInput
{
    public string Provider { get; set; } = "Facebook";
    public string Scope { get; set; } = "general";

    [Required, MaxLength(50000)]
    public string Body { get; set; } = "";
    public long ExpectedActiveId { get; set; }
}
