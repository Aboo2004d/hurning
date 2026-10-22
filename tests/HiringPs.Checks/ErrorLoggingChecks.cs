using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using ElmahCore;
using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Playwright;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class ErrorLoggingChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            count++;
            Console.WriteLine("PASS " + description);
        }
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Integrations:Google:ClientSecret"] = "known-private-value",
            ["ConnectionStrings:Hiring"] = "Server=.;Database=test;Password=sql-private-value",
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Fixture").AddCookie("Fixture", options => options.LoginPath = "/Account/Login");
        var initialProviders = builder.Services.Count(x => x.ServiceType == typeof(ILoggerProvider));
        builder.Services.AddHiringElmah();
        Check(initialProviders == builder.Services.Count(x => x.ServiceType == typeof(ILoggerProvider)),
            "ELMAH registration adds no ILogger provider or legacy bridge");
        builder.Services.RemoveAll<ErrorLog>();
        builder.Services.AddSingleton<ErrorLog, MemoryErrorLog>();
        await using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<ElmahOptions>>().Value;
        Check(!options.LogRequestBody, "Request body capture disabled");
        Check(options.SqlServerDatabaseTableName == "ELMAH_Error", "Native ELMAH SQL table selected");
        var policy = app.Services.GetRequiredService<ElmahErrorPolicy>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/ar/Account/Login";
        context.TraceIdentifier = "trace-probe";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Name, "member@example.test"),
            new Claim(ClaimTypes.NameIdentifier, "member-42"),
            new Claim(ClaimTypes.Email, "member@example.test"),
            new Claim(ClaimTypes.Role, "Admin")], "Fixture"));
        var probe = new Error(new IOException("known-private-value Password=sql-private-value; Bearer abc.def.ghi"));
        probe.QueryString["code"] = "private-query";
        probe.Form["password"] = "private-form";
        probe.Cookies["session"] = "private-cookie";
        probe.ServerVariables["Authorization"] = "private-header";
        probe.Detail += "\n" + new string('x', 70000) + "\nEND-OF-FULL-DETAIL";
        policy.Describe(probe, context);
        var xml = ErrorXml.EncodeString(probe);
        Check(xml.Contains("known-private-value") && xml.Contains("abc.def.ghi") && !xml.Contains("sql-private-value"),
            "Only passwords redacted; other diagnostic text retained");
        Check(xml.Contains("private-query") && !xml.Contains("private-form") && xml.Contains("private-cookie") && xml.Contains("private-header"),
            "Native request context retained with password fields masked");
        Check(probe.User == "member@example.test" && probe.ServerVariables["UserId"] == "member-42"
            && probe.ServerVariables["UserEmail"] == "member@example.test" && probe.ServerVariables["UserRoles"] == "Admin",
            "User identity and roles retained in native error details");
        Check(probe.Detail.Length > 70000 && probe.Detail.EndsWith("END-OF-FULL-DETAIL"), "Full exception details are not truncated");
        var cleaner = app.Services.GetRequiredService<ErrorLogSanitizer>();
        foreach (var text in new[]
        {
            "Password=secret-value; User=admin", "{\"password\":\"a long secret; value\",\"name\":\"member\"}",
            "<Password>secret-value</Password>", "https://test/login?password=secret-value&user=member",
        })
        {
            var cleaned = cleaner.Clean(text);
            Check(!cleaned.Contains("secret-value") && !cleaned.Contains("a long secret; value") && cleaned.Contains("[REDACTED]"),
                "Password syntax masked without dropping surrounding data");
        }
        Check(probe.Detail.Contains("IOException"), "Exception type retained");
        Check(probe.ServerVariables["TraceId"] == "trace-probe", "Tracking reference retained");
        Check(probe.ServerVariables["Path"] == "/ar/Account/Login", "Request path retained");
        Check(probe.Time.Kind == DateTimeKind.Utc && probe.ServerVariables["PalestineTime"]!.Contains("توقيت فلسطين"),
            "UTC storage with canonical Palestine time metadata");
        var access = typeof(HiringPs.Web.Areas.Admin.Controllers.SystemErrorsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Check(access?.Roles == "Admin", "Navigation redirect restricted to administrators");
        Check(LanguageUrls.IsInfrastructure("/elmah/api/errors"), "ELMAH resource URLs bypass language redirects");
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=.;Database=metadata").Options);
        Check(!db.Model.GetEntityTypes().Any(x => x.GetTableName() == "ApplicationErrors"), "Old error entity removed from EF");
        Check(!db.Database.HasPendingModelChanges(), "Migration snapshot matches current model");

        // Fixture identities only; production uses the existing Identity cookie middleware.
        app.UseExceptionHandler(handler => handler.Run(ctx => { ctx.Response.StatusCode = 500; return ctx.Response.WriteAsync("safe error"); }));
        app.UseAuthentication();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Headers.TryGetValue("X-Fixture-Role", out var role))
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "fixture"), new Claim(ClaimTypes.NameIdentifier, "user-42"), new Claim(ClaimTypes.Role, role!)], "Fixture"));
            await next(ctx);
        });
        app.UseHiringElmah();
        app.Run(ctx =>
        {
            if (ctx.Request.Path == "/Errors/Status")
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "text/html";
                return ctx.Response.WriteAsync("<p>Status 500 without an exception</p>");
            }
            throw new InvalidOperationException("fixture failure known-private-value Password=fixture-password;", new IOException("inner failure details"));
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        foreach (var path in new[] { "/elmah/", "/elmah/api/errors", "/elmah/xml?id=probe", "/elmah/js/app.8cac919f.js" })
        {
            using var denied = await client.GetAsync(path);
            Check(denied.StatusCode == HttpStatusCode.Redirect
                && denied.Headers.Location!.ToString().Contains("/Account/Login"), "Anonymous blocked: " + path);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Fixture-Role", "Member");
            using var member = await client.SendAsync(request);
            Check(member.StatusCode == HttpStatusCode.Forbidden, "Member blocked: " + path);
        }
        client.DefaultRequestHeaders.Add("X-Fixture-Role", "Admin");
        using var page = await client.GetAsync("/elmah/");
        var html = await page.Content.ReadAsStringAsync();
        Check(page.IsSuccessStatusCode && html.Contains("ELMAH", StringComparison.OrdinalIgnoreCase), "Administrator receives native ELMAH UI");
        Check(page.Headers.CacheControl?.NoStore == true && page.Headers.Contains("X-Robots-Tag"), "Native diagnostic UI is private and non-cacheable");
        using var activity = new Activity("fixture-request").Start();
        using var failure = await client.GetAsync("/fail?code=private-query");
        Check(failure.StatusCode == HttpStatusCode.InternalServerError, "Native middleware preserves safe exception handler response");
        var entries = new List<ErrorLogEntry>();
        var log = app.Services.GetRequiredService<ErrorLog>();
        log.GetErrors(0, 50, entries);
        Check(entries.Count == 1, "Unhandled web exception recorded once by ELMAH");
        var saved = log.GetError(entries.Single().Id).Error;
        Check(saved.Detail.Contains("fixture failure known-private-value") && !ErrorXml.EncodeString(saved).Contains("fixture-password")
            && saved.Detail.Contains("inner failure details") && saved.User == "fixture",
            "Native persisted error includes user, inner exception and full message with password masked");
        Check(saved.ServerVariables["Path"] == "/fail", "Captured path excludes query string");
        Check(!string.IsNullOrEmpty(saved.ServerVariables["TraceId"]), "Native error retains tracking reference");
        using var api = await client.GetAsync("/elmah/api/errors?s=50");
        Check(api.IsSuccessStatusCode && (await api.Content.ReadAsStringAsync()).Contains("inner failure details"), "Native ELMAH API lists saved errors");
        using var detail = await client.GetAsync("/elmah/api/error?id=" + entries.Single().Id);
        var detailJson = await detail.Content.ReadAsStringAsync();
        Check(detail.IsSuccessStatusCode && detailJson.Contains("inner failure details") && detailJson.Contains("fixture")
            && !detailJson.Contains("fixture-password"), "Native details API exposes full exception and user without password");
        using var statusOnly = await client.GetAsync("/Errors/Status?code=500");
        var afterStatus = new List<ErrorLogEntry>();
        log.GetErrors(0, 50, afterStatus);
        Check(statusOnly.StatusCode == HttpStatusCode.InternalServerError && afterStatus.Count == entries.Count,
            "Opening an explicit rendered status 500 page is not an exception and does not add a false error");
        if (File.Exists("/usr/bin/chromium"))
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = "/usr/bin/chromium", Headless = true, Args = ["--no-sandbox"] });
            var browserContext = await browser.NewContextAsync(new()
            {
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Fixture-Role"] = "Admin" },
                TimezoneId = "Asia/Hebron",
            });
            var browserPage = await browserContext.NewPageAsync();
            var browserErrors = new List<string>();
            browserPage.PageError += (_, error) => browserErrors.Add(error);
            browserPage.Console += (_, message) => { if (message.Type == "error" && message.Text.Contains("Content Security")) browserErrors.Add(message.Text); };
            await browserPage.GotoAsync(address + "/elmah/");
            await browserPage.GetByText("inner failure details", new() { Exact = false }).First.WaitForAsync(new() { Timeout = 15000 });
            Check(browserErrors.Count == 0, "Native ELMAH browser UI works under the scoped CSP: " + string.Join("; ", browserErrors));
            Check(await browserPage.Locator("#app").InnerTextAsync() != "", "Native ELMAH browser application renders");
            await browserPage.GotoAsync(address + "/elmah/detail/" + entries.Single().Id);
            await browserPage.GetByText("inner failure details", new() { Exact = false }).First.WaitForAsync(new() { Timeout = 15000 });
            var detailText = await browserPage.Locator("body").InnerTextAsync();
            Check(detailText.Contains("fixture") && detailText.Contains("inner failure details") && !detailText.Contains("fixture-password"),
                "Native error details page renders user and full exception while masking password");
        }
        await app.StopAsync();
        Console.WriteLine($"ELMAH: {count} checks passed; native in-memory store used, no SQL writes.");
    }
}
