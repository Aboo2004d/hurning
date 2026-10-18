// تسجيل دخول الإدارة والخروج وتغيير كلمة المرور.
using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class PasswordInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(12)]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}

public class AccountController(
    SignInManager<IdentityUser> signIn,
    UserManager<IdentityUser> users,
    HiringDb db,
    MemberVerification verification,
    HiringPs.Web.Infrastructure.PendingVerificationCookie pendingCookie
) : Controller
{
    public IActionResult Login(string? returnUrl) => View(new LoginInput { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("public")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
            return View(input);
        var user = await users.FindByEmailAsync(input.Email);
        if (user is not null)
        {
            var admin = await users.IsInRoleAsync(user, "Admin");
            var member = admin
                ? null
                : await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
            if (!admin && member is null)
            {
                ModelState.AddModelError("", "LoginFailed");
                return View(input);
            }
            if (member is { Suspended: true })
            {
                if (await users.CheckPasswordAsync(user, input.Password))
                {
                    ViewBag.SuspensionReason = member.SuspensionReason;
                    ModelState.AddModelError("", "AccountSuspended");
                }
                else
                    ModelState.AddModelError("", "LoginFailed");
                return View(input);
            }
            if (member is { Activated: false })
            {
                var checkedPassword = await signIn.CheckPasswordSignInAsync(
                    user,
                    input.Password,
                    true
                );
                if (!checkedPassword.Succeeded)
                {
                    ModelState.AddModelError("", "LoginFailed");
                    return View(input);
                }
                try
                {
                    var request = await verification.CreateAsync(
                        user.Id,
                        ChallengePurpose.Registration,
                        null,
                        HttpContext.RequestAborted
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
        }
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, input.Password, false, true);
            if (result.Succeeded)
                return LocalRedirect(
                    Url.IsLocalUrl(input.ReturnUrl)
                        ? input.ReturnUrl!
                        : Url.Action(
                            "Index",
                            await users.IsInRoleAsync(user, "Admin") ? "Admin" : "Dashboard",
                            new
                            {
                                area = await users.IsInRoleAsync(user, "Admin")
                                    ? "Admin"
                                    : "Member",
                            }
                        )!
                );
        }

        ModelState.AddModelError("", "LoginFailed");
        return View(input);
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public IActionResult ChangePassword() => View(new PasswordInput());

    [HttpPost, Authorize, EnableRateLimiting("public")]
    public async Task<IActionResult> ChangePassword(PasswordInput input)
    {
        if (!ModelState.IsValid)
            return View(input);
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        var result = await users.ChangePasswordAsync(
            user,
            input.CurrentPassword,
            input.NewPassword
        );
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(input);
        }

        await signIn.RefreshSignInAsync(user);
        return await users.IsInRoleAsync(user, "Admin")
            ? RedirectToAction("Index", "Admin", new { area = "Admin" })
            : RedirectToAction("Index", "Dashboard", new { area = "Member" });
    }
}
