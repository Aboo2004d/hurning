using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TelegramBotController(TelegramBotSetupService setup, HiringDb db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await setup.CheckAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(CancellationToken ct)
    {
        var result = await setup.RegisterAsync(ct);
        db.Audit.Add(OperationTracking.Create(OperationArea.Telegram,
            result.Verified ? OperationOutcome.Succeeded : result.Applied ? OperationOutcome.Uncertain : OperationOutcome.Failed,
            "WebhookRegistration", $"State {result.Status.State}; Code {result.Status.FailureCode}; Pending {result.Status.PendingUpdates}; {result.Status.FailureDescription}",
            User.Identity?.Name ?? "Admin"));
        await db.SaveChangesAsync(ct);
        ViewData["RegistrationResult"] = result.Verified ? "TelegramBotRegistered" : result.Applied ? "TelegramBotRegistrationUnverified" : "TelegramBotRegistrationFailed";
        return View("Index", result.Status);
    }
}
