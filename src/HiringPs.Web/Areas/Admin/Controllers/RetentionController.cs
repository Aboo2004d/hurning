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

public class RetentionController : AdminControllerBase
{
    public RetentionController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Retention(CancellationToken ct)
    {
        var policy = await db.RetentionPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
        return View(
            new RetentionEditor
            {
                Enabled = policy.Enabled,
                RevisionDays = policy.RevisionDays,
                DiagnosticDays = policy.DiagnosticDays,
                Version = Convert.ToBase64String(policy.Version),
                Current = policy,
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Retention(
        [Bind("Enabled,RevisionDays,DiagnosticDays,Version")] RetentionEditor input,
        CancellationToken ct
    )
    {
        var policy = await db.RetentionPolicies.SingleAsync(x => x.Id == 1, ct);
        input.Current = policy;
        byte[] version = [];
        try
        {
            version = Convert.FromBase64String(input.Version ?? "");
        }
        catch (FormatException)
        {
            ModelState.AddModelError("", "أعد تحميل الإعدادات.");
        }
        if (!ModelState.IsValid)
            return View(input);
        db.Entry(policy).Property(x => x.Version).OriginalValue = version;
        policy.Enabled = input.Enabled;
        policy.RevisionDays = input.RevisionDays;
        policy.DiagnosticDays = input.DiagnosticDays;
        policy.LastCleanupAt = null;
        Audit(
            "Retention.Updated",
            $"Enabled={policy.Enabled}; revisions={policy.RevisionDays}; diagnostics={policy.DiagnosticDays}"
        );
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "تغيّرت الإعدادات؛ أعد تحميل الصفحة.");
            return View(input);
        }
        TempData["Notice"] = "RetentionSaved";
        return RedirectToAction(nameof(Retention));
    }
}
