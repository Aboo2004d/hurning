using System.Security.Claims;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace HiringPs.Web.Infrastructure;

public static class SocialAuthentication
{
    public const string Cookie = "SocialConnection";
    public static void UseInfrastructureCallbackBase(HttpRequest request)
    {
        // OAuth callback URIs stay stable when the initiating page has /ar or /en.
        var path = request.PathBase.Value ?? "";
        if (path.EndsWith("/ar", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/en", StringComparison.OrdinalIgnoreCase))
            request.PathBase = path[..^3];
    }
    public static void AddSocialAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var builder = services.AddAuthentication().AddCookie(Cookie, options =>
        {
            options.Cookie.Name = "HiringPs.SocialConnection";
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        foreach (var provider in new[] { "Facebook", "LinkedIn", "LinkedInPersonal" })
        {
            var id = SocialPublishing.IsLinkedIn(provider) ? SocialPublishing.LinkedInSetting(config, provider, "ClientId") : config[$"Integrations:{provider}:ClientId"];
            var secret = SocialPublishing.IsLinkedIn(provider) ? SocialPublishing.LinkedInSetting(config, provider, "ClientSecret") : config[$"Integrations:{provider}:ClientSecret"];
            if (string.IsNullOrWhiteSpace(id) || id == "***" || string.IsNullOrWhiteSpace(secret) || secret == "***") continue;
            builder.AddOAuth("Publish" + provider, options =>
            {
                options.ClientId = id; options.ClientSecret = secret;
                options.SignInScheme = Cookie;
                options.CallbackPath = "/integrations/" + provider.ToLowerInvariant() + "/callback";
                options.AuthorizationEndpoint = provider == "Facebook" ? "https://www.facebook.com/" + config["Integrations:Facebook:ApiVersion"] + "/dialog/oauth" : "https://www.linkedin.com/oauth/v2/authorization";
                options.TokenEndpoint = provider == "Facebook" ? "https://graph.facebook.com/" + config["Integrations:Facebook:ApiVersion"] + "/oauth/access_token" : "https://www.linkedin.com/oauth/v2/accessToken";
                options.SaveTokens = true;
                options.Backchannel = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
                options.Scope.Clear();
                foreach (var scope in provider == "Facebook" ? new[] { "pages_show_list", "pages_read_engagement", "pages_manage_posts" }
                    : provider == "LinkedInPersonal" ? new[] { "openid", "profile", "w_member_social" }
                    : new[] { "rw_organization_admin", "w_organization_social" }) options.Scope.Add(scope);
                options.Events.OnCreatingTicket = context =>
                {
                    // Preserve a distinct, short-lived OAuth ticket; never replace the admin login.
                    context.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, provider));
                    return Task.CompletedTask;
                };
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse(); context.Response.Redirect("/Admin/Integrations/Index?failed=true");
                    return Task.CompletedTask;
                };
            });
        }
    }
}

