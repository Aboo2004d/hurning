using System.Security.Claims;
using HiringPs.Core;
using HiringPs.Web.Controllers;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Optional persistence coverage in the disposable SQL database, with a simulated Google identity.
public static class GoogleCompletionSqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new HiringDb(options));
        services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<HiringDb>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = "google-" + suffix + "@invalid.test";
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, suffix),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Name, "Fixture"),
                },
                "Google"
            )
        );
        var info = new ExternalLoginInfo(principal, "Google", suffix, "Google");
        var owner = new IdentityUser { UserName = "owner-" + suffix + "@invalid.test" };
        if (!(await users.CreateAsync(owner)).Succeeded)
            throw new Exception("Fixture user creation failed");
        db.MemberProfiles.Add(
            new MemberProfile
            {
                UserId = owner.Id,
                Mobile = "+441234567891",
                Activated = true,
            }
        );
        await db.SaveChangesAsync();
        var signIn = new FixtureSignIn(users, scope.ServiceProvider, info);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Telegram:Enabled"] = "true",
                    ["Telegram:BotToken"] = "123:" + new string('x', 40),
                    ["Telegram:BotUsername"] = "fixture_bot",
                    ["Telegram:WebhookSecret"] = new string('s', 32),
                }
            )
            .Build();
        var verification = new MemberVerification(
            db,
            new TelegramClient(new HttpClient(new RejectNetwork()), config),
            new MembershipService(db)
        );
        GoogleController Controller() =>
            new(
                signIn,
                users,
                db,
                verification,
                new PendingVerificationCookie(new EphemeralDataProtectionProvider()),
                scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            )
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        var duplicate = Controller();
        var result = await duplicate.Complete(
            new GoogleProfileInput { Name = "Fixture", Mobile = "441234567891" },
            default
        );
        Check(
            result is ViewResult
                && duplicate
                    .ModelState["Mobile"]
                    ?.Errors.Any(x => x.ErrorMessage == "TelegramMobileAlreadyRegistered") == true
                && await users.FindByEmailAsync(email) is null,
            "Duplicate Telegram number redisplays Google completion before creating an Identity account"
        );
        var user = new IdentityUser { UserName = email, Email = email };
        if (
            !(await users.CreateAsync(user)).Succeeded
            || !(await users.AddLoginAsync(user, info)).Succeeded
        )
            throw new Exception("Fixture external login creation failed");
        var profile = new MemberProfile { UserId = user.Id, Mobile = "+441234567892" };
        db.MemberProfiles.Add(profile);
        db.MemberPreferences.Add(new MemberPreference { UserId = user.Id });
        await db.SaveChangesAsync();
        var returning = Controller();
        var rejected = await returning.Complete(
            new GoogleProfileInput { Name = "Fixture", Mobile = "441234567891" },
            default
        );
        Check(
            rejected is ViewResult
                && returning.ModelState["Mobile"]?.Errors.Count > 0
                && profile.Mobile == "+441234567892",
            "Duplicate number cannot overwrite the returning Google member's stored number"
        );
        var form = (ViewResult)await Controller().Complete();
        Check(
            form.Model is GoogleProfileInput { Mobile: "441234567892" },
            "Returning Google profile displays digits without a duplicated plus prefix"
        );
        var accepted = await Controller()
            .Complete(
                new GoogleProfileInput { Name = "Fixture", Mobile = "441234567892" },
                default
            );
        Check(
            accepted
                is RedirectToActionResult { ControllerName: "Registration", ActionName: "Verify" }
                && await db.MemberChallenges.AnyAsync(x =>
                    x.UserId == user.Id
                    && x.ExpectedMobile == profile.Mobile
                    && x.Purpose == ChallengePurpose.Registration
                )
                && !profile.Activated,
            "The current member's own number is accepted and continues to Telegram verification without activation"
        );
    }

    private sealed class FixtureSignIn : SignInManager<IdentityUser>
    {
        private readonly ExternalLoginInfo info;

        public FixtureSignIn(
            UserManager<IdentityUser> users,
            IServiceProvider provider,
            ExternalLoginInfo info
        )
            : base(
                users,
                new HttpContextAccessor(),
                provider.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>(),
                provider.GetRequiredService<IOptions<IdentityOptions>>(),
                provider.GetRequiredService<ILogger<SignInManager<IdentityUser>>>(),
                provider.GetRequiredService<IAuthenticationSchemeProvider>(),
                provider.GetRequiredService<IUserConfirmation<IdentityUser>>()
            ) => this.info = info;

        public override Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(
            string? expectedXsrf = null
        ) => Task.FromResult<ExternalLoginInfo?>(info);
    }

    private sealed class RejectNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) =>
            throw new InvalidOperationException(
                "No external network calls are expected in Google completion SQL checks."
            );
    }
}
