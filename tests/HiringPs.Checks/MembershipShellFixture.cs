using System.Security.Claims;
using System.Text.Encodings.Web;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Test-only endpoints render the actual area layouts without a SQL connection.
public sealed class FixtureAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Fixture-Role"].ToString();
        if (role is not ("Admin" or "Member"))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "fixture"),
            new Claim(ClaimTypes.Name, "Fixture"),
            new Claim(ClaimTypes.Role, role),
        };
        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
                    Scheme.Name
                )
            )
        );
    }
}

[Area("Admin"), Authorize(Roles = "Admin")]
public class AdminShellFixtureController : Controller
{
    public IActionResult Index()
    {
        ViewBag.Ready = Array.Empty<string>();
        ViewBag.Available = Array.Empty<string>();
        return View(
            "/Areas/Admin/Views/Gateways/Index.cshtml",
            new List<PaymentGatewaySetting>
            {
                new() { Key = "Lahza" },
                new() { Key = "JawwalPay" },
            }
        );
    }
}

[Area("Member"), Authorize]
public class MemberShellFixtureController : Controller
{
    public IActionResult Index() =>
        View(
            "/Areas/Member/Views/Dashboard/Index.cshtml",
            new MemberDashboard
            {
                Member = new MemberProfile
                {
                    UserId = "fixture",
                    Name = "Fixture",
                    Activated = true,
                },
            }
        );
}

public class TimeoutShellFixtureController : Controller
{
    public IActionResult Index() =>
        throw new TimeoutException("Private diagnostic must not appear in the response");
}
