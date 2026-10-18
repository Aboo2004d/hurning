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
    public IActionResult Tools() =>
        View(
            "/Areas/Admin/Views/Tools/Tools.cshtml",
            new ToolDashboard { Now = DateTimeOffset.UtcNow }
        );

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

[Area("Member"), Authorize]
public sealed class WalletShellFixtureController(PaymentEnvironment environment) : Controller
{
    public IActionResult Index(bool confirm = false) =>
        View(
            "/Areas/Member/Views/WalletPayments/Index.cshtml",
            new WalletCheckoutPage
            {
                Order = new PaymentOrder
                {
                    Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    AmountIls = 5,
                    Gateway = "JawwalPay",
                    GatewayEnvironment = environment.Name,
                    PlanVersion = new MembershipPlanVersion
                    {
                        PlanId = 1,
                        NameAr = "باقة",
                        NameEn = "Plan",
                        Days = 30,
                    },
                },
                IsSandbox = environment.ShowDevelopmentHints,
                CanInitiate = !confirm,
                CanConfirm = confirm,
                PhoneHint = confirm ? "05••••3456" : null,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            }
        );
}

// This controller exists only in the checks assembly; production never loads it.
public class SessionShellFixtureController : Controller
{
    [HttpPost, Authorize(Roles = "Admin")]
    public IActionResult PlanPrice(PlanInput input) =>
        Json(
            new
            {
                amount = input.PriceIls,
                errors = ModelState[nameof(PlanInput.PriceIls)]?.Errors.Count ?? 0,
            }
        );

    [HttpPost, Authorize]
    public async Task<IActionResult> Issue(
        [FromServices]
            IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> cookieOptions,
        string? legacyPath
    )
    {
        var scheme = Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme;
        if (legacyPath is null)
            await HttpContext.SignInAsync(scheme, User);
        else
        {
            var options = cookieOptions.Get(scheme);
            var ticket = new AuthenticationTicket(User, new AuthenticationProperties(), scheme);
            var cookie = options.Cookie.Build(HttpContext);
            cookie.Path = legacyPath;
            options.CookieManager.AppendResponseCookie(
                HttpContext,
                options.Cookie.Name!,
                options.TicketDataFormat.Protect(ticket),
                cookie
            );
        }
        return NoContent();
    }
}

[Area("Admin"), Authorize(Roles = "Admin")]
public class AdminFormShellFixtureController : Controller
{
    public IActionResult EditPreview(string kind)
    {
        ViewBag.OriginalSources = Array.Empty<string>();
        ViewBag.Plans = new List<MembershipPlanVersion>
        {
            new()
            {
                PlanId = 42,
                NameAr = "باقة تجريبية",
                NameEn = "Test plan",
            },
        };
        return kind switch
        {
            "Plans" => View(
                "/Areas/Admin/Views/Plans/PlanForm.cshtml",
                new PlanInput { Id = 42, PriceIls = 5.75m }
            ),
            "Discounts" => View(
                "/Areas/Admin/Views/Discounts/DiscountForm.cshtml",
                new DiscountCode { Id = 42, PlanId = 42 }
            ),
            "Sources" => View(
                "/Areas/Admin/Views/Sources/SourceForm.cshtml",
                new Source { Id = 42 }
            ),
            "Opportunities" => View(
                "/Areas/Admin/Views/Opportunities/OpportunityForm.cshtml",
                new Opportunity { Id = 42 }
            ),
            _ => BadRequest(),
        };
    }
}
