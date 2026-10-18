using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class SettingsController(
    HiringDb db,
    MemberVerification verification,
    PendingVerificationCookie pending
) : MemberControllerBase(db)
{
    public IActionResult Index() => View(Profile);

    [HttpPost]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        Profile.NotificationsEnabled = false;
        await Db.SaveChangesAsync(ct);
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
            return RedirectToAction("Verify", "Registration", new { area = "" });
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
        var prior = await Db.MemberChallenges.SingleAsync(x => x.Id == authorized.Request.Id, ct);
        if (prior.CompletedAt is not null)
            return Forbid();
        try
        {
            var request = await verification.CreateAsync(
                MemberId,
                ChallengePurpose.ChangeTelegram,
                input.NewMobile,
                ct
            );
            prior.CompletedAt = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync(ct);
            pending.Save(HttpContext, new(request, MemberId, DateTimeOffset.UtcNow.AddMinutes(15)));
            return RedirectToAction("Verify", "Registration", new { area = "" });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(input);
        }
    }
}
