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
        bool closing = false,
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
                Cities = [new("رام الله", "رام الله"), new("غزة", "غزة")],
                Categories = [new("برمجة", "برمجة"), new("هندسة", "هندسة")],
                EmploymentTypes = [new("دوام كامل", "دوام كامل"), new("دوام جزئي", "دوام جزئي")],
                ExperienceLevels = [new("مبتدئ", "مبتدئ"), new("متوسط", "متوسط")],
                AllowedDetails = restricted ? [] : [kind],
                SavedIds = saved ? [1] : [],
                ClosingSoon = closing ? [new OpportunityCardDto
                {
                    Id = 42, Kind = OpportunityKind.Job, Title = "وظيفة تغلق هذا الأسبوع",
                    Organization = "مؤسسة تجريبية", Locations = "غزة", OriginalLanguage = "ar",
                    Deadline = TextRules.PalestineToday().AddDays(2),
                }] : [],
                Items =
                [
                    new OpportunityCardDto
                    {
                        Id = 1,
                        CreatedAt = DateTimeOffset.UtcNow,
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

    // Render DTO-backed administration screens without a database or provider request.
    public IActionResult QualityPreview(string screen)
    {
        var now = DateTimeOffset.UtcNow;
        ViewBag.Inbox = new AdminPage<TelegramInboxSummaryDto>
        {
            Items = [new TelegramInboxSummaryDto(1, now, 1)],
        };
        return screen switch
        {
            "ai" => View("/Areas/Admin/Views/AiUsage/AiUsage.cshtml", new AiUsageDashboard
            {
                PauseStatus = new AiPauseStatusDto(now.AddHours(1), "Daily AI token budget reached."),
            }),
            "messages" => View("/Areas/Admin/Views/Messages/Messages.cshtml", new ContactInbox
            {
                Items = [new ContactMessageSummaryDto(1, "عضو تجريبي", "fixture@example.test", now, false, false)],
                Count = 1, UnreadCount = 1,
            }),
            "message" => View("/Areas/Admin/Views/Messages/Message.cshtml",
                new ContactMessageDetailsDto(1, "عضو تجريبي", "fixture@example.test", "رسالة محلية للاختبار", now, true, false)),
            "reports" => View("/Areas/Admin/Views/Reports/Reports.cshtml", new AdminPage<UserReportSummaryDto>
            {
                Items = [new UserReportSummaryDto(1, 42, "فرصة تجريبية", "incorrect", "تجربة عرض البلاغ", false)],
            }),
            "merges" => View("/Areas/Admin/Views/Merges/Merges.cshtml", new AdminPage<MergeOperationSummaryDto>
            {
                Items = [new MergeOperationSummaryDto(1, 42, 43, now, null)],
            }),
            "notifications" => View("/Areas/Admin/Views/Notifications/Index.cshtml", new AdminPage<MemberMessageSummaryDto>
            {
                Items = [new MemberMessageSummaryDto(1, "fixture", "رسالة اختبار محلية", null, null, "TelegramDeliveryProblem", 1)],
            }),
            "pages" => View("/Areas/Admin/Views/Pages/Pages.cshtml", new List<SitePageSummaryDto> { new("about", now) }),
            "item" => View("/Areas/Admin/Views/Items/Item.cshtml", new SourceItemDetailsDto(
                1, "مصدر تجريبي", "fixture", ProcessingStatus.NeedsReview, "", "https://example.test/source",
                "محتوى مصدر تجريبي", "{}", 1)),
            _ => NotFound(),
        };
    }

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
                    new MemberSubscriptionPeriodDto(
                        1,
                        new MembershipPlanVersionSummaryDto { Id = 1, NameAr = "باقة شهرية", NameEn = "Monthly plan" },
                        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(29)
                    ),
                ],
            }
        );

    public IActionResult Preferences(
        PreferenceInput input,
        string? addCategory,
        string? removeCategory,
        string? addLocation,
        string? removeLocation,
        string? bulkAction = null
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
        switch (bulkAction)
        {
            case "select-categories":
                input.Categories = input
                    .Categories.Concat(input.AvailableCategories)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-categories":
                input.Categories = [];
                break;
            case "select-locations":
                input.Locations = input
                    .Locations.Concat(input.AvailableLocations)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-locations":
                input.Locations = [];
                break;
        }

        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("/Areas/Member/Views/Preferences/_PreferenceEditor.cshtml", input)
            : View("/Areas/Member/Views/Preferences/Index.cshtml", input);
    }

    public IActionResult Settings(bool enabled = true)
    {
        var profile = Profile() with
        {
            NotificationsEnabled = enabled
        };
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
        var model = new AdminPage<SavedOpportunitySummaryDto>
        {
            Pager = new AdminPager { Page = page, TotalRecords = total },
            Items = ids.Skip((page - 1) * AdminPager.PageSize)
                .Take(AdminPager.PageSize)
                .Select(id => new SavedOpportunitySummaryDto
                {
                    Id = id,
                    OpportunityId = id,
                    SavedAt = DateTimeOffset.UtcNow,
                    Kind = id % 2 == 0 ? OpportunityKind.Training : OpportunityKind.Job,
                    Title = "فرصة محفوظة للتجربة " + id,
                    Organization = "مؤسسة تجريبية",
                    Locations = "رام الله",
                    Status = PublicationStatus.Published,
                })
                .ToList(),
        };
        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("/Areas/Member/Views/Saved/_SavedResults.cshtml", model)
            : View("/Areas/Member/Views/Saved/Index.cshtml", model);
    }

    private static MemberAccountSummaryDto Profile() =>
        new()
        {
            UserId = "fixture",
            Name = "Test Member — عضو تجريبي",
            Mobile = "+970599123456",
            Activated = true,
            TelegramLinked = true,
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
            new PaymentOrderSummaryDto
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
                PlanVersion = new PaymentPlanSummaryDto
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
            Plan = new MembershipPlanVersionSummaryDto
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
                Order = new PaymentOrderSummaryDto
                {
                    Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    AmountIls = 5,
                    Gateway = "JawwalPay",
                    GatewayEnvironment = environment.Name,
                    PlanVersion = new PaymentPlanSummaryDto
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
        ViewBag.Plans = kind == "Subscriptions"
            ? (object)new List<MembershipPlanLabelDto> { new(version.Id, version.NameAr, version.NameEn) }
            : new List<MembershipPlanVersion> { version };
        ViewBag.Inbox = new AdminPage<TelegramInboxSummaryDto>
        {
            Items = [new TelegramInboxSummaryDto(1, DateTimeOffset.UtcNow, 0)],
        };
        ViewBag.Ready = new[] { "Lahza", "JawwalPay" };
        ViewBag.Available = new[] { "Lahza" };
        return kind switch
        {
            "Members" => View(
                "/Areas/Admin/Views/Members/Index.cshtml",
                PageOf(
                    new AdminMemberSummaryDto
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
                PageOf(new AdminPlanSummaryDto(1, true, new MembershipPlanVersionSummaryDto
                {
                    Id = version.Id,
                    PlanId = version.PlanId,
                    NameAr = version.NameAr,
                    NameEn = version.NameEn,
                    PriceIls = version.PriceIls,
                    Days = version.Days,
                }))
            ),
            "Subscriptions" => View(
                "/Areas/Admin/Views/Subscriptions/Index.cshtml",
                PageOf(
                    new SubscriptionSummaryDto
                    {
                        UserId = "fixture",
                        PlanVersion = new MembershipPlanLabelDto(version.Id, version.NameAr, version.NameEn),
                        StartsAt = DateTimeOffset.UtcNow,
                        EndsAt = DateTimeOffset.UtcNow.AddDays(30),
                    }
                )
            ),
            "Notifications" => View(
                "/Areas/Admin/Views/Notifications/Index.cshtml",
                PageOf(
                    new MemberMessageSummaryDto(0, "fixture",
                        "تم العثور على فرص جديدة تطابق تفضيلاتك في الموقع. يمكنك فتح الموقع للاطلاع على التفاصيل.",
                        null, null, "", 0)
                )
            ),
            "Discounts" => View(
                "/Areas/Admin/Views/Discounts/Index.cshtml",
                PageOf(
                    new DiscountSummaryDto
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
            Pager = new()
            {
                TotalRecords = 1
            },
        };

    public IActionResult PaymentTrace()
    {
        ViewBag.PaymentTraces = new List<PaymentTraceSummaryDto>
        {
            new()
            {
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
            new PaymentOrderSummaryDto
            {
                AmountIls = 5,
                Gateway = "Lahza",
                GatewayEnvironment = "Sandbox",
                PlanVersion = new PaymentPlanSummaryDto
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
        ViewBag.Plans = new List<MembershipPlanVersionSummaryDto>
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
                new DiscountInput { Id = create ? 0 : 42, PlanId = allPlans || create ? null : 42 }
            ),
            "Sources" => View(
                "/Areas/Admin/Views/Sources/SourceForm.cshtml",
                new SourceInput { Id = 42 }
            ),
            "Opportunities" => View(
                "/Areas/Admin/Views/Opportunities/OpportunityForm.cshtml",
                new OpportunityInput { Id = 42 }
            ),
            _ => BadRequest(),
        };
    }
}

public static class PaymentListingFixtureData
{
    public static AdminPage<PaymentOrderSummaryDto> Page(int page = 1)
    {
        var orders = new List<PaymentOrderSummaryDto>
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

    private static PaymentOrderSummaryDto Order(string gateway, string timestamp) =>
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
            PlanVersion = new PaymentPlanSummaryDto
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
    public IActionResult GoogleButton() => PartialView("/Views/Shared/_GoogleLoginButton.cshtml");

    public IActionResult Verification(bool bot = true) =>
        View(
            "/Views/Registration/Verify.cshtml",
            new VerificationInput
            {
                BotLink = bot ? "https://t.me/fixture_bot?start=fixture" : null,
            }
        );

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

    [HttpPost, TypeFilter(typeof(HiringPs.Web.Infrastructure.GoogleCompletionDiagnostics))]
    public IActionResult Validate(HiringPs.Web.Controllers.GoogleProfileInput input) =>
        Json(
            new
            {
                valid = ModelState.IsValid,
                mobile = ModelState.IsValid ? MembershipRules.Mobile(input.Mobile) : null,
            }
        );
}

[Area("Admin"), Authorize(Roles = "Admin")]
public class SocialTemplateShellFixtureController : Controller
{
    public IActionResult Edit()
    {
        ViewBag.History = new AdminPage<SocialTemplateSummaryDto>
        {
            Items = [new SocialTemplateSummaryDto { Id = 1, Provider = "Facebook", Scope = "general", Number = 1, CreatedAt = DateTimeOffset.UtcNow, Actor = "Fixture", Body = SocialTemplateText.DefaultBody, Active = true }],
            Pager = new AdminPager { TotalRecords = 1 },
        };
        return View("/Areas/Admin/Views/SocialTemplates/Edit.cshtml", new HiringPs.Web.Areas.Admin.Controllers.SocialTemplateInput
        {
            Provider = "Facebook",
            Scope = "general",
            Body = SocialTemplateText.DefaultBody,
            ExpectedActiveId = 1
        });
    }

    public IActionResult Compose() => View("/Areas/Admin/Views/Social/Compose.cshtml", new HiringPs.Web.Areas.Admin.Controllers.SocialCompose
    {
        OpportunityId = 1,
        Provider = "Facebook",
        TemplateVersionId = 1,
        TemplateNumber = 1,
        Opportunity = new SocialOpportunitySummaryDto("وظيفة تجريبية"),
        Text = "وظيفة تجريبية",
        Link = "https://hiring.ps/ar/Opportunities/Details/1",
    });
}
