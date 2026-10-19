using System.Net;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

public static class FacebookPageChecks
{
    public static async Task RunAsync()
    {
        foreach (
            var scenario in new[]
            {
                "valid",
                "wrong-page",
                "wrong-app",
                "no-publish",
                "wrong-target",
                "expired",
                "user-token",
            }
        )
        {
            using var db = new DesignTimeFactory().CreateDbContext([]);
            var handler = new Replies(scenario);
            using var client = new HttpClient(handler);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Integrations:Facebook:ApiVersion"] = "v26.0",
                        ["Integrations:Facebook:ClientId"] = "fixture-app",
                        ["Integrations:Facebook:ClientSecret"] = "fixture-secret",
                    }
                )
                .Build();
            var social = new SocialPublishing(
                db,
                config,
                new EphemeralDataProtectionProvider(),
                new Factory(client)
            );
            try
            {
                var result = await social.ResolvePageAsync(
                    "Facebook",
                    "123",
                    "fixture-user-token",
                    default
                );
                if (
                    scenario != "valid"
                    || result.Token != "fixture-page-token"
                    || result.Name != "Fixture page"
                )
                    throw new Exception("Unexpected success: " + scenario);
                Console.WriteLine(
                    "PASS: Empty page list resolves explicit page and validates publishing grant"
                );
            }
            catch (InvalidOperationException ex)
                when (scenario != "valid"
                    && ex.Message is "IntegrationPageInvalid" or "IntegrationPermissionMissing"
                )
            {
                Console.WriteLine("PASS: Direct page fallback rejects " + scenario);
            }
        }
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class Replies(string scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            var path = request.RequestUri!.AbsolutePath;
            object body = path switch
            {
                "/v26.0/me/accounts" => new { data = Array.Empty<object>() },
                "/v26.0/123" => new
                {
                    id = "123",
                    name = "Fixture page",
                    access_token = "fixture-page-token",
                },
                "/v26.0/me" => new { id = scenario == "wrong-page" ? "456" : "123" },
                "/v26.0/debug_token" => new
                {
                    data = new
                    {
                        is_valid = true,
                        app_id = scenario == "wrong-app" ? "other-app" : "fixture-app",
                        type = scenario == "user-token" ? "USER" : "PAGE",
                        expires_at = scenario == "expired" ? 1L : 0L,
                        scopes = scenario == "no-publish"
                            ? Array.Empty<string>()
                            : new[] { "pages_manage_posts" },
                        granular_scopes = new[]
                        {
                            new
                            {
                                scope = "pages_manage_posts",
                                target_ids = new[] { scenario == "wrong-target" ? "456" : "123" },
                            },
                        },
                    },
                },
                _ => throw new Exception("Unexpected provider path: " + path),
            };
            if (
                path == "/v26.0/me"
                && request.Headers.Authorization?.Parameter != "fixture-page-token"
            )
                throw new Exception("Page identity must use the resolved page token");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(body)),
                }
            );
        }
    }
}
