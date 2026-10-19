using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

[EnableRateLimiting("public")]
public class PasswordResetController(
    HiringDb db,
    UserManager<IdentityUser> users,
    MemberVerification verification,
    PendingVerificationCookie pendingCookie
) : Controller
{
    public IActionResult Index() => View(new ResetInput());

    [HttpPost]
    public async Task<IActionResult> Index(ResetInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);
        pendingCookie.Clear(HttpContext);
        var mobile = MembershipRules.Mobile(input.Mobile);
        var matches = await db
            .MemberProfiles.Where(x =>
                x.Mobile == mobile
                && x.Activated
                && !x.Suspended
                && x.TelegramChatId != null
                && x.TelegramUserId != null
            )
            .Take(2)
            .ToListAsync(ct);
        // A verified number must identify exactly one account; never guess if data is ambiguous.
        var member = matches.Count == 1 ? matches[0] : null;
        if (member is not null)
        {
            try
            {
                var request = await verification.CreateAsync(
                    member.UserId,
                    ChallengePurpose.PasswordReset,
                    null,
                    ct
                );
                pendingCookie.Save(
                    HttpContext,
                    new(request, member.UserId, DateTimeOffset.UtcNow.AddMinutes(15))
                );
            }
            catch (Exception ex) when (ex is InvalidOperationException or TelegramDeliveryException)
            { }
        }
        // Same visible result for unknown, inactive and eligible numbers.
        TempData["Notice"] = "ResetRequested";
        return RedirectToAction(nameof(Code));
    }

    public IActionResult Code() => View(new VerificationInput());

    [HttpPost]
    public async Task<IActionResult> Code(VerificationInput input, CancellationToken ct)
    {
        var pending = pendingCookie.Read(HttpContext);
        if (
            pending?.Request.Purpose != ChallengePurpose.PasswordReset
            || !ModelState.IsValid
            || !await verification.VerifyAsync(pending.Request, input.Code, ct)
        )
        {
            ModelState.AddModelError("", "VerificationInvalid");
            return View(input);
        }
        pendingCookie.Save(
            HttpContext,
            pending with
            {
                Verified = true,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            }
        );
        return RedirectToAction(nameof(Complete));
    }

    public IActionResult Complete() =>
        pendingCookie.Read(HttpContext)
            is { Verified: true, Request.Purpose: ChallengePurpose.PasswordReset }
            ? View(new ResetPasswordInput())
            : RedirectToAction(nameof(Index));

    [HttpPost]
    public async Task<IActionResult> Complete(ResetPasswordInput input, CancellationToken ct)
    {
        var pending = pendingCookie.Read(HttpContext);
        if (
            pending is not { Verified: true }
            || pending.Request.Purpose != ChallengePurpose.PasswordReset
        )
            return BadRequest();
        if (!ModelState.IsValid)
            return View(input);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        var challenge = await db
            .MemberChallenges.FromSqlInterpolated(
                $"SELECT * FROM [MemberChallenges] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {pending.Request.Id}"
            )
            .SingleAsync(ct);
        if (challenge.CompletedAt != null || challenge.ConsumedAt == null)
            return BadRequest();
        var user = await users.FindByIdAsync(pending.UserId);
        if (user is null)
            return BadRequest();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "PasswordInvalid");
            return View(input);
        }
        challenge.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        pendingCookie.Clear(HttpContext);
        TempData["Notice"] = "PasswordUpdated";
        return RedirectToAction("Login", "Account");
    }
}
