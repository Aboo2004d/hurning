using System.Security.Claims;
using System.Text.Encodings.Web;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

// Render the public listing for responsive checks without touching production data.
public class ListingShellFixtureController : Controller
{
    public IActionResult Index(
        string? q = null,
        string? city = null,
        string? category = null,
        string? employment = null,
        string? experience = null,
        OpportunityKind kind = OpportunityKind.Job,
        bool archive = false,
        bool restricted = false,
        bool saved = false,
        int page = 1
    ) =>
        View(
            "/Views/Home/Index.cshtml",
            new ListingModel
            {
                Kind = kind,
                Archive = archive,
                Query = q ?? "",
                City = city ?? "",
                Category = category ?? "",
                Employment = employment ?? "",
                Experience = experience ?? "",
                Page = Math.Clamp(page, 1, 2),
                Count = 24,
                Cities = ["رام الله", "غزة"],
                Categories = ["برمجة", "هندسة"],
                EmploymentTypes = ["دوام كامل", "دوام جزئي"],
                ExperienceLevels = ["مبتدئ", "متوسط"],
                AllowedDetails = restricted ? [] : [kind],
                SavedIds = saved ? [1] : [],
                Items =
                [
                    new Opportunity
                    {
                        Id = 1,
                        Kind = kind,
                        Title = "فرصة عمل في مجال تطوير البرمجيات",
                        Organization = "مؤسسة تجريبية",
                        OriginalLanguage = "ar",
                        Locations = "رام الله",
                        Category = "برمجة",
                        Summary =
                            "بيانات عرض تجريبية للتأكد من تنسيق بطاقات الفرص والأزرار والفلاتر.",
                        Deadline = new DateOnly(2026, 11, 1),
                    },
                ],
            }
        );
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
                Member = Profile(),
                SavedCount = 11,
                BotReady = true,
                Subscriptions =
                [
                    new MemberSubscription
                    {
                        StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
                        EndsAt = DateTimeOffset.UtcNow.AddDays(29),
                        PlanVersion = new MembershipPlanVersion
                        {
                            NameAr = "باقة شهرية",
                            NameEn = "Monthly plan",
                        },
                    },
                ],
            }
        );

    public IActionResult Preferences(
        PreferenceInput input,
        string? addCategory,
        string? removeCategory,
        string? addLocation,
        string? removeLocation
    )
    {
        if (HttpMethods.IsGet(Request.Method))
        {
            input.Categories = ["برمجة", "فئة سابقة لم تعد في القائمة"];
            input.Locations = ["رام الله"];
            input.Kinds = [OpportunityKind.Job];
        }
        input.AvailableCategories = ["برمجة", "هندسة", "Design & UX <sample>"];
        input.AvailableLocations = ["رام الله", "غزة", "القدس"];
        if (addCategory is not null)
            input.Categories = input.Categories.Append(addCategory).Distinct().ToArray();
        if (removeCategory is not null)
            input.Categories = input.Categories.Where(x => x != removeCategory).ToArray();
        if (addLocation is not null)
            input.Locations = input.Locations.Append(addLocation).Distinct().ToArray();
        if (removeLocation is not null)
            input.Locations = input.Locations.Where(x => x != removeLocation).ToArray();
        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("/Areas/Member/Views/Preferences/_PreferenceEditor.cshtml", input)
            : View("/Areas/Member/Views/Preferences/Index.cshtml", input);
    }

    public IActionResult Settings(bool enabled = true)
    {
        var profile = Profile();
        profile.NotificationsEnabled = enabled;
        return View("/Areas/Member/Views/Settings/Index.cshtml", profile);
    }

    public IActionResult Saved(int page = 1, int total = 11, int removed = 0)
    {
        page = AdminPager.Normalize(page, total);
        ViewBag.AllowedDetails = new HashSet<OpportunityKind> { OpportunityKind.Job };
        var ids = Enumerable
            .Range(1, 12)
            .Where(id => id != removed)
            .Take(total)
            .OrderByDescending(id => id);
        var model = new AdminPage<SavedOpportunity>
        {
            Pager = new AdminPager { Page = page, TotalRecords = total },
            Items = ids.Skip((page - 1) * AdminPager.PageSize)
                .Take(AdminPager.PageSize)
                .Select(id => new SavedOpportunity
                {
                    Id = id,
                    OpportunityId = id,
                    SavedAt = DateTimeOffset.UtcNow,
                    Opportunity = new Opportunity
                    {
                        Id = id,
                        Kind = id % 2 == 0 ? OpportunityKind.Training : OpportunityKind.Job,
                        Title = "فرصة محفوظة للتجربة " + id,
                        Organization = "مؤسسة تجريبية",
                        Locations = "رام الله",
                        Status = PublicationStatus.Published,
                    },
                })
                .ToList(),
        };
        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("/Areas/Member/Views/Saved/_SavedResults.cshtml", model)
            : View("/Areas/Member/Views/Saved/Index.cshtml", model);
    }

    private static MemberProfile Profile() =>
        new()
        {
            UserId = "fixture",
            Name = "Test Member — عضو تجريبي",
            Mobile = "+970599123456",
            Activated = true,
            TelegramChatId = 1,
            NotificationsEnabled = true,
        };
}

public class TimeoutShellFixtureController : Controller
{
    public IActionResult Index() =>
        throw new TimeoutException("Private diagnostic must not appear in the response");
}

[Area("Member"), Authorize]
public sealed class WalletShellFixtureController(PaymentEnvironment environment) : Controller
{
    public IActionResult PaymentList(int page = 1) =>
        Request.Headers["X-Payment-Page"] == "true"
            ? PartialView(
                "/Areas/Member/Views/Payments/_PaymentTable.cshtml",
                PaymentListingFixtureData.Page(page)
            )
            : View(
                "/Areas/Member/Views/Payments/Index.cshtml",
                PaymentListingFixtureData.Page(page)
            );

    public IActionResult Details(bool paid = false, string phase = "ready")
    {
        ViewBag.Ready = true;
        TempData["PaymentTraceId"] = "11112222333344445555666677778888";
        return View(
            "/Areas/Member/Views/Payments/Details.cshtml",
            new PaymentOrder
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                State =
                    paid ? PaymentState.Paid
                    : phase == "failed" ? PaymentState.Failed
                    : PaymentState.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                CheckoutStartedAt = phase is "returned" or "checked"
                    ? DateTimeOffset.UtcNow.AddMinutes(-2)
                    : null,
                LastCheckedAt = phase is "returned" or "checked"
                    ? DateTimeOffset.UtcNow.AddMinutes(-1)
                    : null,
                AmountIls = 5,
                Gateway = "Lahza",
                GatewayEnvironment = environment.Name,
                PlanVersion = new MembershipPlanVersion
                {
                    PlanId = 1,
                    NameAr = "باقة",
                    NameEn = "Plan",
                    Days = 30,
                },
            }
        );
    }

    public IActionResult PurchasePreview(string? code = null)
    {
        if (code == "INVALID")
            ModelState.AddModelError("DiscountCode", "DiscountInvalid");
        var input = new PurchaseInput
        {
            PlanId = 1,
            Plan = new MembershipPlanVersion
            {
                Id = 1,
                PlanId = 1,
                NameAr = "باقة شهرية",
                NameEn = "Monthly plan",
                Days = 30,
                PriceIls = 5,
            },
            PresentedVersionId = 1,
            PresentedAmount =
                code == "SAVE20" ? 4
                : code == "FREE" ? 0
                : 5,
            FinalAmount =
                code == "SAVE20" ? 4
                : code == "FREE" ? 0
                : 5,
            DiscountCode = code ?? "",
            Gateway = "JawwalPay",
            Gateways = ["JawwalPay", "Lahza"],
            StartsAt = DateTimeOffset.UtcNow,
        };
        return Request.Headers["X-Checkout-Async"] == "true"
            ? PartialView("/Areas/Member/Views/Plans/_CheckoutReview.cshtml", input)
            : View("/Areas/Member/Views/Plans/Review.cshtml", input);
    }

    public IActionResult Index(
        bool confirm = false,
        bool uncertain = false,
        bool rejected = false
    ) =>
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
                CanInitiate = !confirm && !uncertain && !rejected,
                CanConfirm = confirm && !uncertain && !rejected,
                ConfirmationStarted = uncertain || rejected,
                Rejected = rejected,
                CanCancel = !uncertain,
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
    public IActionResult PaymentList(int page = 1) =>
        Request.Headers["X-Payment-Page"] == "true"
            ? PartialView(
                "/Areas/Admin/Views/Payments/_PaymentTable.cshtml",
                PaymentListingFixtureData.Page(page)
            )
            : View(
                "/Areas/Admin/Views/Payments/Index.cshtml",
                PaymentListingFixtureData.Page(page)
            );

    public IActionResult ListPreview(string kind)
    {
        var version = new MembershipPlanVersion
        {
            Id = 1,
            PlanId = 1,
            NameAr = "باقة شهرية",
            NameEn = "Monthly plan",
            Days = 30,
            PriceIls = 5,
        };
        ViewBag.Versions = new List<MembershipPlanVersion> { version };
        ViewBag.Plans = new List<MembershipPlanVersion> { version };
        ViewBag.Inbox = new AdminPage<TelegramInbox>
        {
            Items = [new() { Id = 1, ReceivedAt = DateTimeOffset.UtcNow }],
        };
        ViewBag.Ready = new[] { "Lahza", "JawwalPay" };
        ViewBag.Available = new[] { "Lahza" };
        return kind switch
        {
            "Members" => View(
                "/Areas/Admin/Views/Members/Index.cshtml",
                PageOf(
                    new MemberProfile
                    {
                        UserId = "fixture",
                        Name = "مستخدم تجريبي",
                        Mobile = "0599123456",
                        Activated = true,
                    }
                )
            ),
            "Plans" => View(
                "/Areas/Admin/Views/Plans/Index.cshtml",
                PageOf(
                    new MembershipPlan
                    {
                        Id = 1,
                        CurrentVersionId = 1,
                        Visible = true,
                    }
                )
            ),
            "Subscriptions" => View(
                "/Areas/Admin/Views/Subscriptions/Index.cshtml",
                PageOf(
                    new MemberSubscription
                    {
                        UserId = "fixture",
                        PlanVersion = version,
                        StartsAt = DateTimeOffset.UtcNow,
                        EndsAt = DateTimeOffset.UtcNow.AddDays(30),
                    }
                )
            ),
            "Notifications" => View(
                "/Areas/Admin/Views/Notifications/Index.cshtml",
                PageOf(
                    new MemberMessage
                    {
                        UserId = "fixture",
                        Text =
                            "تم العثور على فرص جديدة تطابق تفضيلاتك في الموقع. يمكنك فتح الموقع للاطلاع على التفاصيل.",
                    }
                )
            ),
            "Discounts" => View(
                "/Areas/Admin/Views/Discounts/Index.cshtml",
                PageOf(
                    new DiscountCode
                    {
                        Id = 1,
                        PlanId = 1,
                        Code = "SAVE10",
                        Percent = 10,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                    }
                )
            ),
            "Gateways" => View(
                "/Areas/Admin/Views/Gateways/Index.cshtml",
                new List<PaymentGatewaySetting>
                {
                    new() { Key = "Lahza", Enabled = true },
                    new() { Key = "JawwalPay" },
                }
            ),
            _ => NotFound(),
        };
    }

    private static AdminPage<T> PageOf<T>(T item) =>
        new()
        {
            Items = [item],
            Pager = new() { TotalRecords = 1 },
        };

    public IActionResult PaymentTrace()
    {
        ViewBag.PaymentTraces = new List<AuditEntry>
        {
            new()
            {
                Action = "Payment.VerifyTrace",
                At = DateTimeOffset.Parse(
                    "2026-10-18T14:34:30Z",
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                Detail =
                    "{\"Stage\":\"ProviderFailure\",\"Code\":\"AmountMismatch\",\"HttpStatus\":200}",
            },
        };
        ViewBag.TraceHost = "Development";
        return View(
            "/Areas/Admin/Views/Payments/Details.cshtml",
            new PaymentOrder
            {
                AmountIls = 5,
                Gateway = "Lahza",
                GatewayEnvironment = "Sandbox",
                PlanVersion = new MembershipPlanVersion
                {
                    PlanId = 1,
                    NameAr = "باقة",
                    NameEn = "Plan",
                    Days = 30,
                },
            }
        );
    }

    public IActionResult EditPreview(string kind, bool allPlans = false, bool create = false)
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
                new DiscountCode { Id = create ? 0 : 42, PlanId = allPlans || create ? null : 42 }
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

public static class PaymentListingFixtureData
{
    public static AdminPage<PaymentOrder> Page(int page = 1)
    {
        var orders = new List<PaymentOrder>
        {
            Order("Lahza", "2026-10-18T14:34:30Z"),
            Order("JawwalPay", "2026-10-18T13:22:00Z"),
        };
        orders.AddRange(
            Enumerable.Range(0, 10).Select(_ => Order("Lahza", "2026-10-17T12:00:00Z"))
        );
        page = AdminPager.Normalize(page, orders.Count);
        return new()
        {
            Pager = new AdminPager { TotalRecords = orders.Count, Page = page },
            Items = orders
                .Skip((page - 1) * AdminPager.PageSize)
                .Take(AdminPager.PageSize)
                .ToList(),
        };
    }

    private static PaymentOrder Order(string gateway, string timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            AmountIls = 5,
            Gateway = gateway,
            GatewayEnvironment = "Sandbox",
            CreatedAt = DateTimeOffset.Parse(
                timestamp,
                System.Globalization.CultureInfo.InvariantCulture
            ),
            PlanVersion = new MembershipPlanVersion
            {
                PlanId = 1,
                NameAr = "باقة",
                NameEn = "Plan",
                Days = 30,
            },
        };
}

// Real Google completion view; no OAuth identity or database access in layout previews.
public sealed class GoogleCompletionShellFixtureController : Controller
{
    public IActionResult Index(bool duplicate = false)
    {
        if (duplicate)
            ModelState.AddModelError("Mobile", "TelegramMobileAlreadyRegistered");
        return View(
            "/Views/Google/Complete.cshtml",
            new HiringPs.Web.Controllers.GoogleProfileInput
            {
                Name = "Fixture member",
                Mobile = duplicate ? "970599123456" : "",
            }
        );
    }

    [HttpPost]
    public IActionResult Validate(HiringPs.Web.Controllers.GoogleProfileInput input) =>
        Json(
            new
            {
                valid = ModelState.IsValid,
                mobile = ModelState.IsValid ? MembershipRules.Mobile(input.Mobile) : null,
            }
        );
}
