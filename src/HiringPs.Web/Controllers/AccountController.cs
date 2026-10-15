// تسجيل دخول الإدارة والخروج وتغيير كلمة المرور.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

public class AccountController(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users) : Controller
{
    public IActionResult Login(string? returnUrl) => View(new LoginInput { ReturnUrl = returnUrl });
    [HttpPost, EnableRateLimiting("public")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
            return View(input);
        var user = await users.FindByEmailAsync(input.Email);
        if (user is not null && await users.IsInRoleAsync(user, "Admin"))
        {
            var result = await signIn.PasswordSignInAsync(user, input.Password, false, true);
            if (result.Succeeded)
                return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/Admin");
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

    [Authorize(Roles = "Admin")]
    public IActionResult ChangePassword() => View(new PasswordInput());
    [HttpPost, Authorize(Roles = "Admin"), EnableRateLimiting("public")]
    public async Task<IActionResult> ChangePassword(PasswordInput input)
    {
        if (!ModelState.IsValid)
            return View(input);
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(input);
        }

        await signIn.RefreshSignInAsync(user);
        return RedirectToAction("Index", "Admin");
    }
}
