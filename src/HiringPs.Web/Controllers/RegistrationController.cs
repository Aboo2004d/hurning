using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

[EnableRateLimiting("public")]
public class RegistrationController(
    HiringDb db,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    MemberVerification verification,
    TelegramClient telegram,
    PendingVerificationCookie pendingCookie
) : Controller
{
    public IActionResult Index()
    {
        ViewBag.BotReady = telegram.Ready;
        return View(new RegisterInput());
    }

    [HttpPost]
    public async Task<IActionResult> Index(RegisterInput input, CancellationToken ct)
    {
        ViewBag.BotReady = telegram.Ready;
        if (!telegram.Ready)
            ModelState.AddModelError("", "TelegramUnavailable");
        string mobile = "";
        try
        {
            mobile = MembershipRules.Mobile(input.Mobile);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(input.Mobile), ex.Message);
        }
        if (!ModelState.IsValid)
            return View(input);
        var user = new IdentityUser { UserName = input.Email.Trim(), Email = input.Email.Trim() };
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "RegistrationFailed");
                return View(input);
            }
            db.MemberProfiles.Add(
                new MemberProfile
                {
                    UserId = user.Id,
                    Name = input.Name.Trim(),
                    Mobile = mobile,
                    Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                }
            );
            db.MemberPreferences.Add(new MemberPreference { UserId = user.Id });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        try
        {
            var request = await verification.CreateAsync(
                user.Id,
                ChallengePurpose.Registration,
                null,
                ct
            );
            pendingCookie.Save(
                HttpContext,
                new(request, user.Id, DateTimeOffset.UtcNow.AddMinutes(15))
            );
            return RedirectToAction(nameof(Verify));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(input);
        }
    }

    public async Task<IActionResult> Recover(Guid id, string token, CancellationToken ct)
    {
        var hash = MemberVerification.Hash(token ?? "");
        var challenge = await db
            .MemberChallenges.AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id
                    && x.LinkHash == hash
                    && x.Purpose == ChallengePurpose.Registration
                    && x.ConsumedAt == null
                    && x.ExpiresAt > DateTimeOffset.UtcNow,
                ct
            );
        if (challenge is null)
            return BadRequest();
        pendingCookie.Save(
            HttpContext,
            new(
                new VerificationRequest(id, token ?? "", ChallengePurpose.Registration),
                challenge.UserId,
                DateTimeOffset.UtcNow.AddMinutes(15)
            )
        );
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return RedirectToAction(nameof(Verify));
    }

    public IActionResult Verify()
    {
        var pending = pendingCookie.Read(HttpContext);
        if (pending is null)
            return RedirectToAction("Login", "Account");
        return View(
            new VerificationInput
            {
                BotLink = pending.Request.Purpose
                    is ChallengePurpose.Registration
                        or ChallengePurpose.ChangeTelegram
                    ? telegram.Link(pending.Request.Token)
                    : null,
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Verify(VerificationInput input, CancellationToken ct)
    {
        var pending = pendingCookie.Read(HttpContext);
        if (pending is null)
            return RedirectToAction("Login", "Account");
        if (!ModelState.IsValid || !await verification.VerifyAsync(pending.Request, input.Code, ct))
        {
            ModelState.AddModelError("", "VerificationInvalid");
            input.BotLink = pending.Request.Purpose
                is ChallengePurpose.Registration
                    or ChallengePurpose.ChangeTelegram
                ? telegram.Link(pending.Request.Token)
                : null;
            return View(input);
        }
        if (
            pending.Request.Purpose
            is ChallengePurpose.PasswordReset
                or ChallengePurpose.AuthorizeTelegramChange
        )
        {
            pendingCookie.Save(
                HttpContext,
                pending with
                {
                    Verified = true,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                }
            );
            return pending.Request.Purpose == ChallengePurpose.PasswordReset
                ? RedirectToAction("Complete", "PasswordReset")
                : RedirectToAction("ReplaceTelegram", "Settings", new { area = "Member" });
        }
        pendingCookie.Clear(HttpContext);
        var user = await users.FindByIdAsync(pending.UserId);
        if (user is null)
            return NotFound();
        await signIn.SignInAsync(user, false);
        return RedirectToAction("Index", "Dashboard", new { area = "Member" });
    }

    [HttpPost]
    public async Task<IActionResult> Resend(CancellationToken ct)
    {
        var pending = pendingCookie.Read(HttpContext);
        if (pending is null)
            return RedirectToAction("Login", "Account");
        try
        {
            var expected = await db
                .MemberChallenges.Where(x => x.Id == pending.Request.Id)
                .Select(x => x.ExpectedMobile)
                .SingleAsync(ct);
            var request = await verification.CreateAsync(
                pending.UserId,
                pending.Request.Purpose,
                expected,
                ct
            );
            pendingCookie.Save(
                HttpContext,
                new(request, pending.UserId, DateTimeOffset.UtcNow.AddMinutes(15))
            );
        }
        catch (Exception ex) when (ex is InvalidOperationException or TelegramDeliveryException)
        {
            TempData["Notice"] =
                ex is TelegramDeliveryException ? "TelegramDeliveryProblem" : ex.Message;
        }
        return RedirectToAction(nameof(Verify));
    }
}
