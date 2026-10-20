using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public class SettingsController(
    HiringDb db,
    MemberVerification verification,
    PendingVerificationCookie pending,
    MemberNotificationSettingsService notifications
) : MemberControllerBase(db)
{
    public IActionResult Index() => View(Profile);

    [HttpPost]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        await notifications.StopAsync(MemberId, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Enable(CancellationToken ct) =>
        await StartAsync(ChallengePurpose.EnableNotifications, ct);

    [HttpPost]
    public async Task<IActionResult> ChangeTelegram(CancellationToken ct) =>
        await StartAsync(ChallengePurpose.AuthorizeTelegramChange, ct);

    private async Task<IActionResult> StartAsync(ChallengePurpose purpose, CancellationToken ct)
    {
        try
        {
            var request = await verification.CreateAsync(MemberId, purpose, null, ct);
            pending.Save(HttpContext, new(request, MemberId, DateTimeOffset.UtcNow.AddMinutes(15)));
            return RedirectToAction("Verify", "Registration", new
            {
                area = ""
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or TelegramDeliveryException)
        {
            TempData["Notice"] =
                ex is TelegramDeliveryException ? "TelegramDeliveryProblem" : ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    public IActionResult ReplaceTelegram() => View(new VerificationInput());

    [HttpPost]
    public async Task<IActionResult> ReplaceTelegram(VerificationInput input, CancellationToken ct)
    {
        var authorized = pending.Read(HttpContext);
        if (
            authorized is not { Verified: true }
            || authorized.UserId != MemberId
            || authorized.Request.Purpose != ChallengePurpose.AuthorizeTelegramChange
        )
            return Forbid();
        if (string.IsNullOrWhiteSpace(input.NewMobile))
        {
            ModelState.AddModelError("", "MobileInternational");
            return View(input);
        }
        try
        {
            var request = await notifications.ReplaceAsync(MemberId, authorized.Request.Id, input.NewMobile, ct);
            if (request is null)
                return Forbid();
            pending.Save(HttpContext, new(request, MemberId, DateTimeOffset.UtcNow.AddMinutes(15)));
            return RedirectToAction("Verify", "Registration", new
            {
                area = ""
            });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(input);
        }
    }
}
