using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

internal static class TelegramBotSetupChecks
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        const string token = "123456789:abcdefghijklmnopqrstuvwx";
        var secret = new string('s', 32);
        var settings = new Dictionary<string, string?>
        {
            ["Telegram:Enabled"] = "true", ["Telegram:BotToken"] = token,
            ["Telegram:BotUsername"] = "HiringFixtureBot", ["Telegram:WebhookSecret"] = secret,
            ["Seo:PublicOrigin"] = "https://hiring.example",
        };
        IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var current = "https://old-tunnel.example/integrations/telegram";
        var username = "HiringFixtureBot";
        var calls = new List<(string Method, string Body)>();
        var reject = false;
        var allowedUpdates = new[] { "message" };
        using var http = new HttpClient(new SetupHandler(async request =>
        {
            var method = request.RequestUri!.Segments.Last();
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync();
            calls.Add((method, body));
            object result;
            if (method == "getMe") result = new { is_bot = true, username };
            else if (method == "getWebhookInfo") result = new
            {
                url = current, pending_update_count = 7, last_error_date = 1793000000,
                last_error_message = "Historical delivery error", allowed_updates = allowedUpdates,
            };
            else if (method == "setWebhook")
            {
                if (reject) return Json(new { ok = false, description = "Rejected " + token + " " + secret }, HttpStatusCode.BadRequest);
                using var payload = JsonDocument.Parse(body);
                Check(payload.RootElement.GetProperty("secret_token").GetString() == secret, "registration sends the configured verification secret");
                Check(!payload.RootElement.GetProperty("drop_pending_updates").GetBoolean(), "registration preserves pending updates");
                Check(payload.RootElement.GetProperty("allowed_updates")[0].GetString() == "message", "message commands are enabled");
                current = payload.RootElement.GetProperty("url").GetString()!;
                allowedUpdates = new[] { "message" };
                result = true;
            }
            else throw new Exception("Unexpected bot operation: " + method);
            return Json(new { ok = true, result });
        }));
        var service = new TelegramBotSetupService(http, Config());
        var state = await service.CheckAsync(default);
        Check(state.State == TelegramBotSetupState.DifferentWebhook && state.PendingUpdates == 7 && state.LastErrorAt is not null,
            "read-only check detects old ngrok address and delivery diagnostics");
        Check(calls.All(x => x.Method is "getMe" or "getWebhookInfo"), "opening setup never registers or sends a message");
        var result = await service.RegisterAsync(default);
        Check(result.Verified && current == "https://hiring.example/integrations/telegram", "activation registers and verifies the actual application callback route");
        Check(calls.All(x => x.Method != "sendMessage" && x.Method != "deleteWebhook"), "setup never sends messages or deletes the webhook queue");
        allowedUpdates = new[] { "callback_query" };
        Check((await service.CheckAsync(default)).State == TelegramBotSetupState.UpdatesDisabled,
            "matching URL does not hide a disabled message update subscription");
        result = await service.RegisterAsync(default);
        Check(result.Verified, "activation restores message updates without replacing the bot identity");
        var previousRegistrations = calls.Count(x => x.Method == "setWebhook");
        result = await service.RegisterAsync(default);
        Check(result.Verified && calls.Count(x => x.Method == "setWebhook") == previousRegistrations + 1,
            "re-registration refreshes verification secret even when the URL already matches");
        var setCount = calls.Count(x => x.Method == "setWebhook");
        username = "AnotherFixtureBot";
        result = await service.RegisterAsync(default);
        Check(result.Status.State == TelegramBotSetupState.IdentityMismatch && calls.Count(x => x.Method == "setWebhook") == setCount,
            "wrong bot identity prevents external registration");
        username = "HiringFixtureBot";
        reject = true;
        result = await service.RegisterAsync(default);
        var serialized = JsonSerializer.Serialize(result);
        Check(!result.Applied && serialized.Contains("redacted") && !serialized.Contains(token) && !serialized.Contains(secret),
            "provider errors cannot expose bot token or webhook secret in returned details");
        foreach (var (key, value) in new[] { ("Telegram:Enabled", "false"), ("Telegram:WebhookSecret", "short"), ("Seo:PublicOrigin", "http://localhost:5186") })
        {
            var old = settings[key]; settings[key] = value;
            var before = calls.Count;
            state = await new TelegramBotSetupService(http, Config()).CheckAsync(default);
            Check(!state.Configured && calls.Count == before, "invalid or disabled settings prevent network requests: " + key);
            settings[key] = old;
        }
        using var failedHttp = new HttpClient(new SetupHandler(_ => throw new HttpRequestException("private URI " + token)));
        state = await new TelegramBotSetupService(failedHttp, Config()).CheckAsync(default);
        Check(state.FailureCode == "Network" && !JsonSerializer.Serialize(state).Contains(token), "network failures do not carry request URIs into diagnostics");
        using var badHttp = new HttpClient(new SetupHandler(_ => Task.FromResult(Json(new { ok = true, result = new[] { "invalid" } }))));
        state = await new TelegramBotSetupService(badHttp, Config()).CheckAsync(default);
        Check(state.FailureCode == "InvalidResponse", "malformed API result is a setup error rather than a page exception");
        Check(typeof(TelegramBotController).GetCustomAttribute<AuthorizeAttribute>()?.Roles == "Admin"
            && typeof(TelegramBotController).GetMethod("Activate")!.IsDefined(typeof(HttpPostAttribute))
            && typeof(TelegramBotController).GetMethod("Activate")!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
            "activation requires admin authorization, POST and antiforgery validation");
        Check(typeof(TelegramWebhookController).GetMethod("Receive")!.GetCustomAttribute<HttpPostAttribute>()!.Template == TelegramBotSetupService.WebhookPath,
            "registration URL and receiving controller share the same route constant");
        reject = false;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Audit (Id INTEGER PRIMARY KEY AUTOINCREMENT, Actor TEXT NOT NULL, Action TEXT NOT NULL, Detail TEXT NOT NULL, At TEXT NOT NULL)");
        var controller = new TelegramBotController(service, db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Fixture admin"), new Claim(ClaimTypes.Role, "Admin")
            }, "fixture")) } }
        };
        var page = (ViewResult)await controller.Activate(default);
        var audit = await db.Audit.SingleAsync();
        Check(audit.Actor == "Fixture admin" && audit.Action == "Activity.Telegram.Succeeded.WebhookRegistration"
            && !audit.Detail.Contains(token) && !audit.Detail.Contains(secret) && page.ViewName == "Index",
            "successful activation renders verified state and records its actor without credentials");
        Console.WriteLine($"Telegram bot setup: {checks} checks passed; all Bot API calls used local mock handlers.");
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    private sealed class SetupHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
}
