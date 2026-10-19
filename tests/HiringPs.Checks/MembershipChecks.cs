using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Controllers;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static class MembershipChecks
{
    private static int count;

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("FAIL: " + name);
        count++;
        Console.WriteLine("PASS: " + name);
    }

    public static async Task RunAsync()
    {
        Check(
            LanguageUrls.Switch(
                "/ar/Home/Contact?q=a%20b&page=3&status=paid&culture=ar#message",
                PathString.Empty,
                "en"
            ) == "/en/Home/Contact?q=a%20b&page=3&status=paid#message",
            "Language switch preserves path, query and fragment"
        );
        Check(
            LanguageUrls.Switch("/hire/ar/Member/Payments?page=2", "/hire", "en")
                == "/hire/en/Member/Payments?page=2",
            "Language switch preserves mount path"
        );
        Check(
            LanguageUrls.WithoutLanguage("/an/Home/Contact") == "/Home/Contact",
            "Mistyped language does not discard continuation"
        );
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "fr;q=1,en-US;q=0.8,ar;q=0.4";
        Check(LanguageUrls.Select(context) == "en", "Supported browser preference");
        context.Request.Headers.Cookie = ".AspNetCore.Culture=c%3Dar%7Cuic%3Dar";
        Check(LanguageUrls.Select(context) == "ar", "Saved language precedes browser preference");
        Check(
            MembershipRules.Mobile("00970599123456") == "+970599123456",
            "International phone normalization"
        );
        Check(
            MembershipRules.Amount(5, 100) == 0 && MembershipRules.Amount(5, 20) == 4,
            "Full and partial discounts"
        );
        Check(
            !MembershipRules.Paid(PaymentState.Pending)
                && MembershipRules.Paid(PaymentState.ManuallyApproved),
            "Only confirmed payment states grant rights"
        );
        var now = DateTimeOffset.UtcNow;
        var subscriptions = new[]
        {
            new MemberSubscription { StartsAt = now.AddDays(-27), EndsAt = now.AddDays(3) },
            new MemberSubscription { StartsAt = now.AddDays(3), EndsAt = now.AddDays(33) },
        };
        Check(
            MembershipRules.Starts(now, subscriptions).AddDays(30) == now.AddDays(63),
            "New purchases append to the whole queue"
        );
        Check(
            MembershipRules.Starts(now, [subscriptions[0]]).AddDays(30) == now.AddDays(33),
            "Three remaining days plus thirty purchased days"
        );
        var preference = new MemberPreference
        {
            Kinds = MembershipRules.Bit(OpportunityKind.Job),
            CategoriesJson = "[\"Programming\",\"Engineering\"]",
            LocationsJson = "[\"رام الله\",\"غزة\"]",
        };
        var opportunity = new Opportunity
        {
            Kind = OpportunityKind.Job,
            Category = "Programming",
            LocationSearchText = LocationSearch.Normalize("رام الله"),
        };
        Check(
            MembershipRules.Matches(preference, opportunity),
            "One selected category and one selected location suffice"
        );
        opportunity.Kind = OpportunityKind.Training;
        Check(!MembershipRules.Matches(preference, opportunity), "Opportunity type is mandatory");
        preference.Kinds = 15;
        preference.CategoriesJson = "[]";
        preference.LocationsJson = "[]";
        Check(
            MembershipRules.Matches(preference, opportunity),
            "Empty category/location preferences match all"
        );
        context.Request.Headers.Cookie = "";
        context.Request.Headers.AcceptLanguage = "invalid;q=not-a-number";
        Check(
            LanguageUrls.Select(context) == "ar",
            "Malformed browser language safely falls back to Arabic"
        );
        Check(
            MembershipRules.DisplayPeriods(subscriptions).Single().EndsAt == now.AddDays(33),
            "Contiguous same-version renewal displays the full remaining duration"
        );
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Telegram:Enabled"] = "true",
                    ["Telegram:BotToken"] = "***",
                    ["Telegram:BotUsername"] = "***",
                }
            )
            .Build();
        Check(
            !new TelegramClient(new HttpClient(), configuration).Ready,
            "Placeholder bot credentials never become ready"
        );
        using (var db = new DesignTimeFactory().CreateDbContext([]))
        {
            Check(
                !db.Database.HasPendingModelChanges(),
                "Migration snapshot matches the runtime model"
            );
            var model =
                db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
            Check(
                model
                    .FindEntityType(typeof(MemberSubscription))!
                    .GetIndexes()
                    .Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "OrderId"),
                "Database prevents duplicate subscription grants per order"
            );
            Check(
                model
                    .FindEntityType(typeof(MemberProfile))!
                    .GetIndexes()
                    .Any(x =>
                        x.IsUnique && x.Properties.SingleOrDefault()?.Name == "TelegramUserId"
                    ),
                "Database prevents sharing a Telegram identity"
            );
        }
        PaymentDisplayChecks.Run();
        CheckoutStateChecks.Run();
        await ShellAsync();
        Console.WriteLine(
            $"Membership checks passed: {count}. No SQL connection, Telegram request or real payment was made."
        );
    }

    public static async Task ShellAsync(
        bool preview = false,
        string environmentName = "Development"
    )
    {
        var root = Path.GetFullPath("src/HiringPs.Web");
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ApplicationName = typeof(HomeController).Assembly.FullName,
                ContentRootPath = root,
                EnvironmentName = environmentName,
            }
        );
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<HiringDb>(x =>
            x.UseSqlServer(
                "Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"
            )
        );
        builder.Services.AddSingleton<SiteSeo>();
        builder.Services.AddSingleton<UrlPolicy>();
        builder.Services.AddScoped<MembershipService>();
        builder.Services.AddScoped<MemberVerification>();
        builder.Services.AddScoped<PendingVerificationCookie>();
        builder.Services.AddScoped<PaymentEvidenceStore>();
        builder.Services.AddScoped<PaymentReconciliation>();
        builder.Services.AddScoped<PaymentTraceWriter>();
        builder.Services.AddScoped<PaymentGatewayRegistry>();
        builder.Services.AddSingleton<PaymentEnvironment>();
        builder.Services.AddScoped<PaymentToolProbes>();
        builder.Services.AddLahza();
        builder.Services.AddJawwalPay();
        builder.Services.AddScoped<WalletCheckoutService>();
        builder.Services.AddHttpClient<TelegramClient>().RemoveAllLoggers();
        builder
            .Services.AddIdentity<
                Microsoft.AspNetCore.Identity.IdentityUser,
                Microsoft.AspNetCore.Identity.IdentityRole
            >()
            .AddEntityFrameworkStores<HiringDb>()
            .AddDefaultTokenProviders();
        builder
            .Services.AddAuthentication(options => options.DefaultAuthenticateScheme = "Fixture")
            .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.ConfigureApplicationCookie(x =>
        {
            x.LoginPath = "/Account/Login";
            x.Cookie.Name = "HiringPs.Admin";
            x.Cookie.Path = "/";
        });
        builder.Services.AddLocalization(x => x.ResourcesPath = "Resources");
        builder
            .Services.AddControllersWithViews(x =>
                x.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())
            )
            .AddApplicationPart(typeof(HomeController).Assembly)
            .AddApplicationPart(typeof(AdminShellFixtureController).Assembly);
        builder.Services.AddRateLimiter(x =>
            x.AddFixedWindowLimiter(
                "public",
                o =>
                {
                    o.PermitLimit = 100;
                    o.Window = TimeSpan.FromMinutes(1);
                }
            )
        );
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseMiddleware<LanguagePathMiddleware>();
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                ExceptionHandlingPath = "/Errors/Status",
                StatusCodeSelector = ex => ex is TimeoutException ? 504 : 500,
            }
        );
        app.UseRequestLocalization(SiteLocalization.Options());
        app.Use(
            async (context, next) =>
            {
                if (context.Request.Headers["X-Fixture-Windows-Arabic"] == "true")
                {
                    var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                    culture.NumberFormat.NumberDecimalSeparator = "٫";
                    culture.NumberFormat.NumberGroupSeparator = "٬";
                    CultureInfo.CurrentCulture = culture;
                }
                await next();
            }
        );
        app.UseStatusCodePagesWithReExecute("/Errors/Status", "?code={0}");
        app.UseStaticFiles();
        app.UseRouting();
        app.Use(
            async (context, next) =>
            {
                if (context.Request.Headers["X-Fixture-Inspect"] == "routes")
                {
                    var action = context
                        .GetEndpoint()
                        ?.Metadata.GetMetadata<ControllerActionDescriptor>();
                    if (action is null)
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }
                    await context.Response.WriteAsJsonAsync(
                        new
                        {
                            controller = action.ControllerName,
                            action = action.ActionName,
                            area = action.RouteValues.TryGetValue("area", out var area)
                                ? area
                                : null,
                            admin = context
                                .GetEndpoint()!
                                .Metadata.GetOrderedMetadata<IAuthorizeData>()
                                .Any(x => x.Roles == "Admin"),
                        }
                    );
                    return;
                }
                await next();
            }
        );
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapSiteRoutes();
        await app.StartAsync();
        await using (var paymentScope = app.Services.CreateAsyncScope())
        {
            var services = paymentScope.ServiceProvider;
            Check(
                services.GetRequiredService<WalletCheckoutService>() is not null,
                "Wallet checkout dependency graph resolves without database access"
            );
            var expected = environmentName == "Development" ? "Sandbox" : "Production";
            Check(
                services.GetRequiredService<LahzaGateway>().Environment == expected
                    && services.GetRequiredService<JawwalPayGateway>().Environment == expected,
                "Both gateway clients receive the runtime payment environment through DI"
            );
        }
        if (preview)
        {
            await File.WriteAllTextAsync("/tmp/hiring-membership-shell-url", app.Urls.Single());
            await app.WaitForShutdownAsync();
            return;
        }
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            var origin = app.Urls.Single();
            var response = await client.GetAsync(origin + "/an/Home/Contact?page=2&q=test");
            Check(
                response.StatusCode == HttpStatusCode.TemporaryRedirect
                    && response
                        .Headers.Location!.ToString()
                        .Contains("/ar/Home/Contact?page=2&q=test"),
                "HTTP mistyped prefix preserves query"
            );
            response = await client.GetAsync(origin + "/en/Errors/Status?code=404");
            var html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.NotFound
                    && html.Contains("Page not found")
                    && html.Contains("lang=\"en\""),
                "Real Razor error view uses explicit English URL"
            );
            Check(
                html.Contains("/en/Account/Login") && !html.Contains("/en/Admin/Admin"),
                "Generated links retain language without duplicate area segments"
            );
            Check(
                html.Contains("hreflang") == false && html.Contains("noindex"),
                "Error pages are not advertised as indexable translations"
            );
            response = await client.GetAsync(origin + "/ar/does-not-exist");
            Check(
                response.StatusCode == HttpStatusCode.NotFound
                    && (await response.Content.ReadAsStringAsync()).Contains("404"),
                "Unknown route re-executes the custom 404 view"
            );
            response = await client.GetAsync(origin + "/en/Account/Login");
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && html.Contains("/en/Registration")
                    && html.Contains("__RequestVerificationToken"),
                "Login page contains registration and CSRF-protected form"
            );
            foreach (
                var path in new[]
                {
                    "/en/Admin",
                    "/en/Admin/Plans",
                    "/en/Admin/Members",
                    "/en/Member/Plans",
                    "/en/Member/Payments",
                }
            )
            {
                response = await client.GetAsync(origin + path);
                Check(
                    response.StatusCode == HttpStatusCode.Redirect
                        && response.Headers.Location!.ToString().Contains("Account/Login"),
                    "Anonymous area protected: " + path
                );
            }
            response = await client.GetAsync(origin + "/en/css/site.css");
            Check(
                response.StatusCode == HttpStatusCode.OK,
                "Static resources work under language prefix"
            );
            response = await client.GetAsync(origin + "/en/Registration");
            html = await response.Content.ReadAsStringAsync();
            var csrf = System
                .Text.RegularExpressions.Regex.Match(
                    html,
                    "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""
                )
                .Groups[1]
                .Value;
            response = await client.PostAsync(
                origin + "/en/Registration",
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["Name"] = "",
                        ["Email"] = "invalid",
                        ["Mobile"] = "",
                        ["Password"] = "",
                        ["ConfirmPassword"] = "",
                        ["__RequestVerificationToken"] = csrf,
                    }
                )
            );
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && html.Contains("This field is required")
                    && html.Contains("international number"),
                "Empty registration is validated without database writes or server errors"
            );
            foreach (var pair in AdminActionRoutes.Controllers)
            {
                var type = typeof(HomeController)
                    .Assembly.GetTypes()
                    .Single(x =>
                        x.Name == pair.Value + "Controller"
                        && x.Namespace == "HiringPs.Web.Areas.Admin.Controllers"
                    );
                var methods = type.GetMethods().Where(x => x.Name == pair.Key).ToList();
                Check(methods.Count > 0, "Admin action has a responsible controller: " + pair.Key);
                var postOnly = methods.All(x =>
                    x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0
                );
                using var request = new HttpRequestMessage(
                    postOnly ? HttpMethod.Post : HttpMethod.Get,
                    origin + "/en/Admin/" + pair.Key
                );
                request.Headers.Add("X-Fixture-Inspect", "routes");
                using var inspected = await client.SendAsync(request);
                var routeText = await inspected.Content.ReadAsStringAsync();
                using var route = JsonDocument.Parse(routeText);
                Check(
                    inspected.StatusCode == HttpStatusCode.OK
                        && route.RootElement.GetProperty("controller").GetString() == pair.Value
                        && route.RootElement.GetProperty("area").GetString() == "Admin"
                        && route.RootElement.GetProperty("admin").GetBoolean(),
                    "Legacy admin route resolves with Admin authorization: " + pair.Key
                );
            }
            client.DefaultRequestHeaders.Add("X-Fixture-Role", "Admin");
            await AdminFormChecks.RunAsync(client, origin);
            foreach (var language in new[] { "ar", "en" })
            {
                var list = WebUtility.HtmlDecode(
                    await client.GetStringAsync(
                        origin + "/" + language + "/Admin/AdminFormShellFixture/PaymentList"
                    )
                );
                Check(
                    list.Contains("2026-10-18 17:34")
                        && list.Contains("2026-10-18 16:22")
                        && list.IndexOf("2026-10-18 17:34", StringComparison.Ordinal)
                            < list.IndexOf("2026-10-18 16:22", StringComparison.Ordinal),
                    "Admin payment list shows local date columns in supplied newest-first order: "
                        + language
                );
                Check(
                    list.Contains(language == "ar" ? "لحظة" : "Lahza")
                        && list.Contains(language == "ar" ? "جوال باي" : "Jawwal"),
                    "Admin payment rows label both gateways: " + language
                );
            }
            response = await client.GetAsync(
                origin + "/ar/Admin/AdminFormShellFixture/PaymentTrace"
            );
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.IsSuccessStatusCode
                    && html.Contains(PaymentTraceWriter.BuildMarker)
                    && html.Contains("AmountMismatch")
                    && html.Contains("payment-diagnostics")
                    && html.Contains("2026-10-18 17:34:30"),
                "Admin payment page renders protected diagnostic history and current build marker"
            );
            response = await client.GetAsync(origin + "/en/Admin/AdminShellFixture");
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && html.Contains("/en/Admin/Plans")
                    && html.Contains("Lahza"),
                "Admin area renders its own navigation and localized plan links"
            );
            Check(!html.Contains("beta-banner-inner"), "Admin layout omits the public beta banner");
            response = await client.GetAsync(origin + "/en/Account/ChangePassword");
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && (await response.Content.ReadAsStringAsync()).Contains("/en/Admin/Plans"),
                "Admin password page can render the area navigation from a public controller"
            );
            client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            client.DefaultRequestHeaders.Add("X-Fixture-Role", "Member");
            response = await client.GetAsync(origin + "/ar/Member/MemberShellFixture");
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && html.Contains("/ar/Member/Plans")
                    && html.Contains("/ar/Account/Logout"),
                "Member area has its own navigation and logout"
            );
            foreach (var language in new[] { "ar", "en" })
            {
                var list = await client.GetStringAsync(
                    origin + "/" + language + "/Member/WalletShellFixture/PaymentList"
                );
                list = WebUtility.HtmlDecode(list);
                Check(
                    list.Contains("2026-10-18 17:34")
                        && list.Contains("2026-10-18 16:22")
                        && list.Contains(language == "ar" ? "لحظة" : "Lahza")
                        && list.Contains(language == "ar" ? "جوال باي" : "Jawwal"),
                    "Member payment rows show both providers and Palestine dates: " + language
                );
                var payment = await client.GetStringAsync(
                    origin + "/" + language + "/Member/WalletShellFixture/Details"
                );
                Check(
                    payment.Contains("payment-actions")
                        && payment.Contains("button button-outline")
                        && !payment.Contains("id=\"payment-support\"")
                        && !payment.Contains("/Payments/Check")
                        && payment.Contains("11112222333344445555666677778888"),
                    "Payment controls and safe trace reference render in " + language
                );
                var paid = await client.GetStringAsync(
                    origin + "/" + language + "/Member/WalletShellFixture/Details?paid=true"
                );
                Check(
                    !paid.Contains("name=\"evidence\"") && !paid.Contains("/Member/Plans/Review"),
                    "Paid orders no longer invite a second payment or activation complaint"
                );
            }
            response = await client.GetAsync(origin + "/ar/Account/ChangePassword");
            Check(
                response.StatusCode == HttpStatusCode.OK
                    && (await response.Content.ReadAsStringAsync()).Contains("/ar/Member/Plans"),
                "Member password page can render its own area navigation"
            );
            client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            foreach (var role in new[] { "Admin", "Member" })
            foreach (var language in new[] { "ar", "en" })
            {
                var cookies = new CookieContainer();
                using var session = new HttpClient(
                    new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies }
                );
                session.DefaultRequestHeaders.Add("X-Fixture-Role", role);
                var page =
                    role == "Admin" ? "/Admin/AdminShellFixture" : "/Member/MemberShellFixture";
                var shell = await session.GetStringAsync(origin + "/" + language + page);
                var logoutForm = System
                    .Text.RegularExpressions.Regex.Match(
                        shell,
                        "<form[^>]*action=\"[^\"]*/Account/Logout\"[^>]*>.*?</form>",
                        System.Text.RegularExpressions.RegexOptions.Singleline
                    )
                    .Value;
                var token = System
                    .Text.RegularExpressions.Regex.Match(
                        logoutForm,
                        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""
                    )
                    .Groups[1]
                    .Value;
                Check(token.Length > 0, role + " logout form includes CSRF token in " + language);
                foreach (var path in new[] { "", "/ar", "/en" })
                {
                    var fields = new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = token,
                    };
                    if (path.Length > 0)
                        fields["legacyPath"] = path;
                    using var issued = await session.PostAsync(
                        origin + "/" + language + "/SessionShellFixture/Issue",
                        new FormUrlEncodedContent(fields)
                    );
                    Check(
                        issued.StatusCode == HttpStatusCode.NoContent,
                        "Issue current or legacy login cookie: " + path
                    );
                }
                Check(
                    cookies
                        .GetAllCookies()
                        .Cast<Cookie>()
                        .Any(c => c.Name == "HiringPs.Admin" && c.Path == "/"),
                    "Login cookie uses a shared path independent of language"
                );
                var otherLanguage = language == "ar" ? "en" : "ar";
                // Language navigation renders a fresh form with that page's CSRF cookie.
                shell = await session.GetStringAsync(origin + "/" + otherLanguage + page);
                logoutForm = System
                    .Text.RegularExpressions.Regex.Match(
                        shell,
                        "<form[^>]*action=\"[^\"]*/Account/Logout\"[^>]*>.*?</form>",
                        System.Text.RegularExpressions.RegexOptions.Singleline
                    )
                    .Value;
                token = System
                    .Text.RegularExpressions.Regex.Match(
                        logoutForm,
                        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""
                    )
                    .Groups[1]
                    .Value;
                using var loggedOut = await session.PostAsync(
                    origin + "/" + otherLanguage + "/Account/Logout",
                    new FormUrlEncodedContent(
                        new Dictionary<string, string> { ["__RequestVerificationToken"] = token }
                    )
                );
                Check(
                    loggedOut.StatusCode == HttpStatusCode.Redirect
                        && loggedOut.Headers.Location?.ToString() == "/" + otherLanguage,
                    role + " logout redirects home after switching from " + language
                );
                Check(
                    !cookies.GetAllCookies().Cast<Cookie>().Any(c => c.Name == "HiringPs.Admin"),
                    "Logout removes root and both legacy language cookies for " + role
                );
            }
            await AccountScheduleChecks.RunAsync(client, origin);
            await GoogleCompletionFormChecks.RunAsync(client, origin);
            response = await client.GetAsync(origin + "/en/TimeoutShellFixture");
            html = await response.Content.ReadAsStringAsync();
            Check(
                response.StatusCode == HttpStatusCode.GatewayTimeout
                    && html.Contains("The request timed out")
                    && !html.Contains("Private diagnostic"),
                "Timeout is a localized 504 without internal exception text"
            );
            response = await client.PostAsync(
                origin + "/integrations/telegram",
                new StringContent("{}")
            );
            Check(
                response.StatusCode == HttpStatusCode.Unauthorized,
                "Webhook rejects missing/placeholder secret before database access"
            );
            response = await client.PostAsync(
                origin + "/integrations/lahza/webhook",
                new StringContent("{}")
            );
            Check(
                response.StatusCode == HttpStatusCode.Unauthorized,
                "Lahza webhook rejects placeholders without language redirect or SQL"
            );
            var callbackOrder = Guid.NewGuid();
            foreach (var language in new[] { "ar", "en" })
            {
                response = await client.GetAsync(
                    origin
                        + "/integrations/lahza/return?reference="
                        + LahzaGateway.Reference(callbackOrder)
                        + "&lang="
                        + language
                );
                Check(
                    response.StatusCode == HttpStatusCode.Redirect
                        && response.Headers.Location?.OriginalString
                            == "/" + language + "/Member/Payments/Returned/" + callbackOrder,
                    "Public Lahza callback preserves explicit "
                        + language
                        + " and navigates to protected member verification"
                );
            }
            response = await client.GetAsync(
                origin + "/integrations/lahza/return?reference=invalid&lang=en"
            );
            Check(
                response.StatusCode == HttpStatusCode.BadRequest,
                "HTTP callback rejects malformed reference"
            );
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
