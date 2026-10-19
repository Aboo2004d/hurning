using System.Net;
using HiringPs.Core;
using HiringPs.Web.Controllers;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Optional: exercises real recovery lookup/challenge persistence in the disposable SQL database.
public static class PasswordRecoverySqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options)
    {
        await using var db = new HiringDb(options);
        var id = Guid.NewGuid().ToString();
        var member = new MemberProfile
        {
            UserId = id,
            Mobile = "+441234567890",
            Activated = true,
            TelegramChatId = 777,
            TelegramUserId = 777,
        };
        db.Users.Add(new IdentityUser { Id = id, UserName = id + "@invalid.test" });
        db.MemberProfiles.Add(member);
        await db.SaveChangesAsync();
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
        using var handler = new TelegramStub();
        using var http = new HttpClient(handler);
        var verification = new MemberVerification(
            db,
            new TelegramClient(http, config),
            new MembershipService(db)
        );
        var cookie = new PendingVerificationCookie(new EphemeralDataProtectionProvider());
        async Task Request(string number, bool eligible)
        {
            var context = new DefaultHttpContext();
            var controller = new PasswordResetController(db, null!, verification, cookie)
            {
                ControllerContext = new ControllerContext { HttpContext = context },
                TempData = new TempDataDictionary(context, new NoTempData()),
            };
            var before = await db.MemberChallenges.CountAsync(x => x.UserId == id);
            var result = await controller.Index(new ResetInput { Mobile = number }, default);
            var after = await db.MemberChallenges.CountAsync(x => x.UserId == id);
            if (
                result is not RedirectToActionResult { ActionName: "Code" }
                || (string?)controller.TempData["Notice"] != "ResetRequested"
                || after - before != (eligible ? 1 : 0)
            )
                throw new Exception("FAIL: Phone recovery eligibility or uniform response");
        }
        await Request("441234567891", false);
        member.Activated = false;
        await db.SaveChangesAsync();
        await Request("441234567890", false);
        member.Activated = true;
        member.Suspended = true;
        await db.SaveChangesAsync();
        await Request("441234567890", false);
        member.Suspended = false;
        member.TelegramChatId = null;
        await db.SaveChangesAsync();
        await Request("441234567890", false);
        member.TelegramChatId = 777;
        await db.SaveChangesAsync();
        await Request("441234567890", true);
        var challenge = await db.MemberChallenges.SingleAsync(x => x.UserId == id);
        if (
            handler.Sends != 1
            || challenge.ExpectedMobile != member.Mobile
            || challenge.Purpose != ChallengePurpose.PasswordReset
        )
            throw new Exception(
                "FAIL: Phone recovery challenge must match the registered linked account"
            );
        Console.WriteLine(
            "PASS: SQL recovery uses exact registered phone, activation and Telegram link, with the same visible result for ineligible numbers"
        );
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TelegramStub : HttpMessageHandler
    {
        public int Sends { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Sends++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}"),
                }
            );
        }
    }
}
