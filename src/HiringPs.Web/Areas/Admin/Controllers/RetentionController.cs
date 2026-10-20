using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class RetentionController : AdminControllerBase
{
    public RetentionController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        RetentionSettingsService service
    )
        : base(database, urls, settings) => RetentionSettings = service;

    private RetentionSettingsService RetentionSettings
    {
        get;
    }

    public async Task<IActionResult> Retention(CancellationToken ct)
    {
        var policy = await RetentionSettings.ReadAsync(ct);
        return View(new RetentionEditor
        {
            Enabled = policy.Enabled,
            RevisionDays = policy.RevisionDays,
            DiagnosticDays = policy.DiagnosticDays,
            Version = Convert.ToBase64String(policy.Version),
            Current = policy,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Retention([Bind("Enabled,RevisionDays,DiagnosticDays,Version")] RetentionEditor input, CancellationToken ct)
    {
        input.Current = await RetentionSettings.ReadAsync(ct);
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
        var result = await RetentionSettings.SaveAsync(new(input.Enabled, input.RevisionDays, input.DiagnosticDays, version),
            User.Identity?.Name ?? "Admin", ct);
        if (result == RecordUpdateOutcome.Conflict)
        {
            input.Current = input.Current with
            {
                LastCleanupAt = null
            };
            ModelState.AddModelError("", "تغيّرت الإعدادات؛ أعد تحميل الصفحة.");
            return View(input);
        }
        TempData["Notice"] = "RetentionSaved";
        return RedirectToAction(nameof(Retention));
    }
}
