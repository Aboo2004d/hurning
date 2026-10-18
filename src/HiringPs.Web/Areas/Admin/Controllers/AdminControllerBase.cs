using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : Controller
{
    protected readonly HiringDb db;
    protected readonly UrlPolicy urlPolicy;
    protected readonly IConfiguration configuration;

    protected AdminControllerBase(HiringDb database, UrlPolicy urls, IConfiguration settings)
    {
        db = database;
        urlPolicy = urls;
        configuration = settings;
    }

    protected void Audit(string action, string detail) =>
        db.Audit.Add(
            new()
            {
                Actor = User.Identity?.Name ?? "Admin",
                Action = action,
                Detail = detail,
            }
        );

    protected static void NormalizeStrings(object input)
    {
        foreach (
            var property in input
                .GetType()
                .GetProperties()
                .Where(x => x.PropertyType == typeof(string) && x.CanWrite)
        )
            if (property.GetValue(input) is null)
                property.SetValue(input, "");
    }
}
