using System.Net;
using System.Text.Json;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Real Google middleware, simulated provider HTTP responses; no Google account or SQL required.
public static class GoogleRedirectChecks
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        );
        var provider = new ProviderStub();
        builder
            .Services.AddAuthentication("app")
            .AddCookie("app")
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddGoogle(options =>
            {
                options.ClientId = "fixture-client";
                options.ClientSecret = "fixture-secret";
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Backchannel = new HttpClient(provider);
            });
        await using var app = builder.Build();
        // Simulate a nontrusted network peer for the final trust-boundary check only.
        app.Use(
            async (context, next) =>
            {
                if (context.Request.Headers["X-Fixture-Untrusted"] == "true")
                    context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
                await next();
            }
        );
        app.UseForwardedHeaders();
        app.UsePathBase("/hire");
        app.UseAuthentication();
        app.UseMiddleware<LanguagePathMiddleware>();
        app.Run(async context =>
        {
            if (context.Request.Path == "/challenge")
            {
                var properties = new AuthenticationProperties
                {
                    RedirectUri = context.Request.PathBase + "/Google/Callback",
                };
                SocialAuthentication.UseInfrastructureCallbackBase(context.Request);
                await context.ChallengeAsync("Google", properties);
            }
            else
                context.Response.StatusCode = 404;
        });
        await app.StartAsync();
        var origin = app.Urls.Single();
        using var client = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
        );
        var count = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new Exception("FAIL: " + label);
            count++;
            Console.WriteLine("PASS: " + label);
        }
        foreach (var mount in new[] { "", "/hire" })
        foreach (var language in new[] { "ar", "en" })
        foreach (
            var publicHost in new string?[]
            {
                null,
                "fixture.ngrok-free.dev",
                "hiring.ps",
                "staging.example.test:8443",
            }
        )
        {
            var proxy = publicHost is not null;
            var label =
                mount
                + "/"
                + language
                + (proxy ? " via HTTPS proxy on " + publicHost : " direct HTTP");
            HttpRequestMessage Request(string path)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, origin + path);
                if (proxy)
                {
                    request.Headers.Host = publicHost;
                    request.Headers.Add("X-Forwarded-Proto", "https");
                }
                return request;
            }
            using var challenge = await client.SendAsync(
                Request(mount + "/" + language + "/challenge")
            );
            var location = challenge.Headers.Location;
            Check(
                challenge.StatusCode == HttpStatusCode.Redirect
                    && location?.Host == "accounts.google.com",
                "Google challenge starts: " + label
            );
            var query = QueryHelpers.ParseQuery(location!.Query);
            var expected = (proxy ? "https://" + publicHost : origin) + mount + "/signin-google";
            Check(
                query["redirect_uri"] == expected,
                "Callback has public scheme and mount without language: " + label
            );
            var correlation = challenge
                .Headers.GetValues("Set-Cookie")
                .Single(x => x.StartsWith(".AspNetCore.Correlation."));
            Check(
                correlation.Contains(
                    "path=" + mount + "/signin-google;",
                    StringComparison.OrdinalIgnoreCase
                ),
                "Correlation cookie targets the actual callback: " + label
            );
            using var callbackRequest = Request(
                mount
                    + "/signin-google?code=fixture-code&state="
                    + Uri.EscapeDataString(query["state"].ToString())
            );
            callbackRequest.Headers.Add("Cookie", correlation.Split(';')[0]);
            using var callback = await client.SendAsync(callbackRequest);
            Check(
                callback.StatusCode == HttpStatusCode.Redirect
                    && callback.Headers.Location?.OriginalString
                        == mount + "/" + language + "/Google/Callback"
                    && provider.TokenRedirect == expected,
                "OAuth exchange uses the same callback and returns to the original language: "
                    + label
            );
        }
        using var untrusted = new HttpRequestMessage(HttpMethod.Get, origin + "/ar/challenge");
        untrusted.Headers.Add("X-Fixture-Untrusted", "true");
        untrusted.Headers.Add("X-Forwarded-Proto", "https");
        using var rejected = await client.SendAsync(untrusted);
        Check(
            QueryHelpers
                .ParseQuery(rejected.Headers.Location!.Query)["redirect_uri"]
                .ToString()
                .StartsWith("http://", StringComparison.Ordinal),
            "Untrusted peer cannot spoof the forwarded HTTPS scheme"
        );
        await app.StopAsync();
        Console.WriteLine(
            "Google redirect checks passed: "
                + count
                + ". Provider responses are simulated; no real Google request was sent."
        );
    }

    private sealed class ProviderStub : HttpMessageHandler
    {
        public string? TokenRedirect { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                TokenRedirect = QueryHelpers.ParseQuery(body)["redirect_uri"];
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"fixture-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"fixture-id\",\"email\":\"fixture@example.invalid\",\"name\":\"Fixture\"}"
                ),
            };
        }
    }
}
