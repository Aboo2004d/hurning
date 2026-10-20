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

[EnableRateLimiting("public")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GoogleController(
    SignInManager<IdentityUser> signIn,
    UserManager<IdentityUser> users,
    HiringDb db,
    MemberVerification verification,
    PendingVerificationCookie pendingCookie,
    IAuthenticationSchemeProvider schemes
) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Start(bool link = false)
    {
        if (await schemes.GetSchemeAsync("Google") is null)
            return NotFound();
        if (await db.SocialConnections.AnyAsync(x => x.Provider == "Google" && !x.Enabled))
            return Failure("IntegrationDisabled");
        if (User.IsInRole("Admin"))
            return Forbid();
        if (link && User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");
        var userId = link ? users.GetUserId(User) : null;
        var properties = signIn.ConfigureExternalAuthenticationProperties(
            "Google",
            Url.Action(nameof(Callback), new { link }),
            userId
        );
        SocialAuthentication.UseInfrastructureCallbackBase(Request);
        return Challenge(properties, "Google");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Disconnect()
    {
        if (User.IsInRole("Admin"))
            return Forbid();
        var user = await users.GetUserAsync(User);
        if (user is null || !await users.HasPasswordAsync(user))
            return Forbid();
        var logins = await users.GetLoginsAsync(user);
        var login = logins.SingleOrDefault(x => x.LoginProvider == "Google");
        if (login is not null)
        {
            var result = await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
            if (!result.Succeeded)
                return BadRequest();
            await signIn.RefreshSignInAsync(user);
        }
        return RedirectToAction("Index", "Settings", new { area = "Member" });
    }

    public async Task<IActionResult> Callback(bool link = false, string? remoteError = null)
    {
        if (remoteError is not null)
            return Failure("GoogleFailed");
        if (await db.SocialConnections.AnyAsync(x => x.Provider == "Google" && !x.Enabled))
            return Failure("IntegrationDisabled");
        if (link && (User.Identity?.IsAuthenticated != true || User.IsInRole("Admin")))
            return Forbid();
        var info = await signIn.GetExternalLoginInfoAsync(link ? users.GetUserId(User) : null);
        if (info?.LoginProvider != "Google")
            return Failure("GoogleFailed");
        if (link)
        {
            var current = await users.GetUserAsync(User);
            if (
                current is null
                || !await db.MemberProfiles.AnyAsync(x =>
                    x.UserId == current.Id && x.Activated && !x.Suspended
                )
            )
                return Forbid();
            var result = await users.AddLoginAsync(current, info);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            TempData["Notice"] = result.Succeeded ? "GoogleLinked" : "GoogleFailed";
            return RedirectToAction("Index", "Settings", new { area = "Member" });
        }
        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is null)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return Failure("GoogleFailed");
            if (await users.FindByEmailAsync(email) is not null)
                return Failure("GoogleExistingAccount");
            return RedirectToAction(nameof(Complete));
        }
        if (await users.IsInRoleAsync(user, "Admin") || await users.IsLockedOutAsync(user))
            return Forbid();
        var profile = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (profile is null || profile.Suspended)
            return Forbid();
        if (!profile.Activated)
            return RedirectToAction(nameof(Complete));
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signIn.SignInAsync(user, false, "Google");
        return RedirectToAction("Index", "Dashboard", new { area = "Member" });
    }

    public async Task<IActionResult> Complete()
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info?.LoginProvider != "Google")
            return Failure("GoogleFailed");
        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        var profile = user is null
            ? null
            : await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (
            user is not null
            && (
                profile is null
                || profile.Activated
                || profile.Suspended
                || await users.IsInRoleAsync(user, "Admin")
            )
        )
            return Forbid();
        return View(
            new GoogleProfileInput
            {
                Name = profile?.Name ?? info.Principal.FindFirstValue(ClaimTypes.Name) ?? "",
                Mobile = profile?.Mobile.TrimStart('+') ?? "",
            }
        );
    }

    [HttpPost, TypeFilter(typeof(GoogleCompletionDiagnostics))]
    public async Task<IActionResult> Complete(GoogleProfileInput input, CancellationToken ct)
    {
        if (await db.SocialConnections.AnyAsync(x => x.Provider == "Google" && !x.Enabled, ct))
            return Failure("IntegrationDisabled");
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info?.LoginProvider != "Google")
            return Failure("GoogleFailed");
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
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return Failure("GoogleFailed");
        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        var currentUserId = user?.Id;
        if (
            await db.MemberProfiles.AnyAsync(
                x => x.Mobile == mobile && x.UserId != currentUserId,
                ct
            )
        )
        {
            ModelState.AddModelError(nameof(input.Mobile), "TelegramMobileAlreadyRegistered");
            return View(input);
        }
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (user is null)
            {
                if (await users.FindByEmailAsync(email) is not null)
                    return Failure("GoogleExistingAccount");
                user = new IdentityUser { UserName = email, Email = email };
                if (!(await users.CreateAsync(user)).Succeeded)
                    return Failure("GoogleFailed");
                if (!(await users.AddLoginAsync(user, info)).Succeeded)
                    return Failure("GoogleFailed");
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
            }
            else
            {
                var profile = await db.MemberProfiles.SingleOrDefaultAsync(
                    x => x.UserId == user.Id,
                    ct
                );
                if (
                    profile is null
                    || profile.Activated
                    || profile.Suspended
                    || await users.IsInRoleAsync(user, "Admin")
                )
                    return Forbid();
                profile.Name = input.Name.Trim();
                profile.Mobile = mobile;
                // Invalidate any earlier uncompleted registration links for the old number.
                await db
                    .MemberChallenges.Where(x =>
                        x.UserId == user.Id
                        && x.Purpose == ChallengePurpose.Registration
                        && x.ConsumedAt == null
                    )
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.ConsumedAt, DateTimeOffset.UtcNow),
                        ct
                    );
            }
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
            return RedirectToAction("Verify", "Registration");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(input);
        }
    }

    private IActionResult Failure(string code)
    {
        TempData["Notice"] = code;
        return RedirectToAction("Login", "Account");
    }
}
