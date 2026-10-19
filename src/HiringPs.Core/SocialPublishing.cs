using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public sealed class SocialConnection
{
    [Key, MaxLength(20)] public string Provider { get; set; } = "";
    public bool Enabled { get; set; } = true;
    [MaxLength(100)] public string PageId { get; set; } = "";
    [MaxLength(300)] public string PageName { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialPublication
{
    public long? TemplateVersionId { get; set; }
    public Guid Id { get; set; }
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    [MaxLength(20)] public string Provider { get; set; } = "";
    [MaxLength(100)] public string PageId { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public string Text { get; set; } = "";
    [MaxLength(500)] public string RemoteId { get; set; } = "";
    [MaxLength(1500)] public string Url { get; set; } = "";
    [MaxLength(256)] public string Actor { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// Read-only probes and publishing use the same page identity and protected credentials.
public sealed class SocialPublishing(HiringDb db, IConfiguration config,
    IDataProtectionProvider protection, IHttpClientFactory clients, ILogger<SocialPublishing>? logger = null)
{
    private readonly IDataProtector protector = protection.CreateProtector("HiringPs.SocialToken.v1");
    public static bool IsProvider(string provider) => provider is "Facebook" or "LinkedIn" or "LinkedInPersonal";
    public static bool IsLinkedIn(string provider) => provider is "LinkedIn" or "LinkedInPersonal";
    public static string TemplateProvider(string provider) => provider == "LinkedInPersonal" ? "LinkedIn" : provider;
    public static string LinkedInSetting(IConfiguration config, string provider, string name) =>
        config[$"Integrations:{provider}:{name}"] ?? config[$"Integrations:LinkedIn:{name}"] ?? "";
    private string RequiredLinkedIn(string provider, string name)
    {
        var value = LinkedInSetting(config, provider, name);
        return !string.IsNullOrWhiteSpace(value) && value != "***" ? value
            : throw new InvalidOperationException("IntegrationNotConfigured");
    }
    public string Protect(string token) => protector.Protect(token);
    public static bool Eligible(Opportunity item, DateOnly today) =>
        item.Status == PublicationStatus.Published && (item.Deadline is null || item.Deadline >= today) && string.IsNullOrWhiteSpace(item.ClosureEvidence);
    public string OpportunityUrl(long id, string language)
    {
        var origin = config["Seo:PublicOrigin"]?.TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("PublicOriginRequired");
        return origin + (language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "/en" : "/ar") + "/Opportunities/Details/" + id;
    }
    private HttpClient Client() => clients.CreateClient("SocialProviders");
    private string FacebookRoot => "https://graph.facebook.com/" + Required("Integrations:Facebook:ApiVersion");
    private string Required(string key) => !string.IsNullOrWhiteSpace(config[key]) && config[key] != "***" ? config[key]! : throw new InvalidOperationException("IntegrationNotConfigured");
    private async Task<JsonDocument> ReadAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await Client().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger?.LogWarning("SocialConnection Stage ProviderRequest Path {Path} HttpStatus {HttpStatus}",
                request.RequestUri?.AbsolutePath, (int)response.StatusCode);
            throw new InvalidOperationException("IntegrationProviderRejected");
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private HttpRequestMessage Request(string url, string token, string provider = "LinkedIn")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (url.StartsWith("https://api.linkedin.com/rest/"))
        {
            request.Headers.Add("LinkedIn-Version", RequiredLinkedIn(provider, "ApiVersion"));
            request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
        }
        return request;
    }
    public async Task<(string Token, string Name)> ResolvePageAsync(string provider, string pageId, string token, CancellationToken ct)
    {
        if (!IsProvider(provider) || !System.Text.RegularExpressions.Regex.IsMatch(pageId, "^[0-9]{1,40}$")) throw new InvalidOperationException("IntegrationPageInvalid");
        if (provider == "Facebook")
        {
            using var request = Request(FacebookRoot + "/me/accounts?fields=id,name,access_token,tasks&limit=100", token);
            using var pages = await ReadAsync(request, ct);
            foreach (var page in pages.RootElement.GetProperty("data").EnumerateArray())
            {
                if (page.GetProperty("id").GetString() != pageId) continue;
                if (!page.GetProperty("tasks").EnumerateArray().Any(x => x.GetString() is "CREATE_CONTENT" or "MANAGE" or "PROFILE_PLUS_CREATE_CONTENT")) throw new InvalidOperationException("IntegrationPermissionMissing");
                return (page.GetProperty("access_token").GetString()!, page.GetProperty("name").GetString()!);
            }
            // Some business-managed pages are omitted from /me/accounts despite a page grant.
            // Resolve only the explicitly selected page, then validate its own token.
            logger?.LogInformation("SocialConnection Provider Facebook Stage DirectPageLookup PageId {PageId}", pageId);
            using var directRequest = Request(FacebookRoot + "/" + pageId + "?fields=id,name,access_token", token);
            using var direct = await ReadAsync(directRequest, ct);
            var selected = direct.RootElement;
            if (selected.GetProperty("id").GetString() != pageId)
                throw new InvalidOperationException("IntegrationPageInvalid");
            var pageToken = selected.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(pageToken))
                throw new InvalidOperationException("IntegrationPageUnavailable");
            await ValidateFacebookPageTokenAsync(pageId, pageToken, ct);
            return (pageToken, selected.GetProperty("name").GetString() ?? pageId);
        }
        using var aclRequest = Request("https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&state=APPROVED", token);
        using var acl = await ReadAsync(aclRequest, ct);
        var urn = "urn:li:organization:" + pageId;
        if (!acl.RootElement.GetProperty("elements").EnumerateArray().Any(x => x.GetProperty("organization").GetString() == urn && x.GetProperty("role").GetString() is "ADMINISTRATOR" or "CONTENT_ADMINISTRATOR")) throw new InvalidOperationException("IntegrationPermissionMissing");
        using var orgRequest = Request("https://api.linkedin.com/rest/organizations/" + pageId, token);
        using var org = await ReadAsync(orgRequest, ct);
        return (token, org.RootElement.GetProperty("localizedName").GetString() ?? pageId);
    }
    public async Task<(string Id, string Name)> ResolvePersonalAsync(string token, CancellationToken ct)
    {
        using var request = Request("https://api.linkedin.com/v2/userinfo", token);
        using var response = await ReadAsync(request, ct);
        var profile = response.RootElement;
        var id = profile.GetProperty("sub").GetString() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,100}$"))
            throw new InvalidOperationException("IntegrationPageInvalid");
        return (id, profile.TryGetProperty("name", out var name) ? name.GetString() ?? id : id);
    }
    public async Task ValidateLinkedInGrantAsync(string provider, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.linkedin.com/oauth/v2/introspectToken")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token, ["client_id"] = RequiredLinkedIn(provider, "ClientId"),
                ["client_secret"] = RequiredLinkedIn(provider, "ClientSecret")
            })
        };
        using var grant = await ReadAsync(request, ct);
        var permission = provider == "LinkedInPersonal" ? "w_member_social" : "w_organization_social";
        if (!grant.RootElement.GetProperty("active").GetBoolean()
            || !grant.RootElement.GetProperty("scope").GetString()!.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Contains(permission))
            throw new InvalidOperationException("IntegrationPermissionMissing");
    }
    private async Task ValidateFacebookPageTokenAsync(string pageId, string token, CancellationToken ct)
    {
        using var identityRequest = Request(FacebookRoot + "/me?fields=id", token);
        using var identity = await ReadAsync(identityRequest, ct);
        if (identity.RootElement.GetProperty("id").GetString() != pageId)
            throw new InvalidOperationException("IntegrationPageInvalid");
        logger?.LogInformation("SocialConnection Provider Facebook Stage ValidatePageGrant PageId {PageId}", pageId);
        using var request = Request(FacebookRoot + "/debug_token?input_token=" + Uri.EscapeDataString(token),
            Required("Integrations:Facebook:ClientId") + "|" + Required("Integrations:Facebook:ClientSecret"));
        using var response = await ReadAsync(request, ct);
        var grant = response.RootElement.GetProperty("data");
        if (!grant.GetProperty("is_valid").GetBoolean()
            || grant.GetProperty("app_id").GetString() != Required("Integrations:Facebook:ClientId")
            || grant.GetProperty("type").GetString() != "PAGE"
            || !grant.GetProperty("scopes").EnumerateArray().Any(x => x.GetString() == "pages_manage_posts")
            || (grant.TryGetProperty("expires_at", out var expiry) && expiry.GetInt64() != 0
                && expiry.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            throw new InvalidOperationException("IntegrationPermissionMissing");
        if (grant.TryGetProperty("granular_scopes", out var scopes))
            foreach (var scope in scopes.EnumerateArray())
                if (scope.GetProperty("scope").GetString() == "pages_manage_posts"
                    && scope.TryGetProperty("target_ids", out var targets)
                    && !targets.EnumerateArray().Any(x => x.GetString() == pageId))
                    throw new InvalidOperationException("IntegrationPermissionMissing");
    }
    public async Task<ToolProbe> ProbeAsync(string provider, CancellationToken ct)
    {
        if (provider == "GoogleLogin")
        {
            if (string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"]) || config["Authentication:Google:ClientId"] == "***" || string.IsNullOrWhiteSpace(config["Authentication:Google:ClientSecret"]) || config["Authentication:Google:ClientSecret"] == "***")
                return new(provider, ToolState.NotConfigured, "IntegrationNotConfigured");
            if (await db.SocialConnections.AnyAsync(x => x.Provider == "Google" && !x.Enabled, ct))
                return new(provider, ToolState.Disabled, "IntegrationDisabled");
            try
            {
                var callback = OpportunityUrl(0, "ar").Split("/ar/")[0] + "/signin-google";
                using var response = await Client().GetAsync("https://accounts.google.com/.well-known/openid-configuration", ct);
                return new(provider, response.IsSuccessStatusCode ? ToolState.Configured : ToolState.Problem, "GoogleConfigurationOnly", callback);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException) { return new(provider, ToolState.Problem, "IntegrationUnavailable"); }
        }
        var connection = await db.SocialConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Provider == provider, ct);
        if (connection is null) return new(provider, ToolState.NotConfigured, "IntegrationNotConfigured");
        try
        {
            if (!connection.Enabled) return new(provider, ToolState.Disabled, "IntegrationDisabled");
            if (connection.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("IntegrationExpired");
            var token = protector.Unprotect(connection.ProtectedToken);
            if (provider == "Facebook")
            {
                using var request = Request(FacebookRoot + "/" + connection.PageId + "?fields=id,name", token);
                using var page = await ReadAsync(request, ct);
                if (page.RootElement.GetProperty("id").GetString() != connection.PageId) throw new InvalidOperationException("IntegrationPageInvalid");
                // Token debugger confirms the grant; a page read alone does not prove publish permission.
                using var debug = Request(FacebookRoot + "/debug_token?input_token=" + Uri.EscapeDataString(token), Required("Integrations:Facebook:ClientId") + "|" + Required("Integrations:Facebook:ClientSecret"));
                using var grant = await ReadAsync(debug, ct);
                var data = grant.RootElement.GetProperty("data");
                if (!data.GetProperty("is_valid").GetBoolean()
                    || data.GetProperty("app_id").GetString() != Required("Integrations:Facebook:ClientId")
                    || !data.GetProperty("scopes").EnumerateArray().Any(x => x.GetString() == "pages_manage_posts")) throw new InvalidOperationException("IntegrationPermissionMissing");
            }
            else
            {
                if (provider == "LinkedInPersonal")
                {
                    var profile = await ResolvePersonalAsync(token, ct);
                    if (profile.Id != connection.PageId) throw new InvalidOperationException("IntegrationPageInvalid");
                }
                else await ResolvePageAsync(provider, connection.PageId, token, ct);
                await ValidateLinkedInGrantAsync(provider, token, ct);
            }
            return new(provider, ToolState.Ready, provider == "LinkedInPersonal" ? "IntegrationPersonalReady" : "IntegrationPageReady", connection.PageName + " · " + connection.PageId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or JsonException or KeyNotFoundException or System.Security.Cryptography.CryptographicException)
        { return new(provider, ToolState.Problem, ex is InvalidOperationException ? ex.Message : "IntegrationUnavailable", connection.PageName + " · " + connection.PageId); }
    }
    public async Task PublishAsync(SocialPublication attempt, CancellationToken ct)
    {
        var connection = await db.SocialConnections.AsNoTracking().SingleAsync(x => x.Provider == attempt.Provider, ct);
        if (!connection.Enabled) throw new InvalidOperationException("IntegrationDisabled");
        if (connection.PageId != attempt.PageId) throw new InvalidOperationException("IntegrationPageInvalid");
        if (connection.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("IntegrationExpired");
        var token = protector.Unprotect(connection.ProtectedToken);
        using var request = Request(attempt.Provider == "Facebook" ? FacebookRoot + "/" + connection.PageId + "/feed" : "https://api.linkedin.com/rest/posts", token, attempt.Provider);
        request.Method = HttpMethod.Post;
        request.Content = attempt.Provider == "Facebook"
            ? new FormUrlEncodedContent(new Dictionary<string,string> { ["message"] = attempt.Text })
            : JsonContent.Create(new { author = (attempt.Provider == "LinkedInPersonal" ? "urn:li:person:" : "urn:li:organization:") + connection.PageId, commentary = attempt.Text,
                visibility = "PUBLIC", distribution = new { feedDistribution = "MAIN_FEED", targetEntities = Array.Empty<object>(), thirdPartyDistributionChannels = Array.Empty<object>() }, lifecycleState = "PUBLISHED", isReshareDisabledByAuthor = false });
        using var response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        logger?.LogInformation("SocialPublish Attempt {AttemptId} Provider {Provider} Stage ResponseHeaders HttpStatus {HttpStatus}",
            attempt.Id, attempt.Provider, (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            attempt.Status = (int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ? "Unknown" : "Failed";
            return;
        }
        if (attempt.Provider == "Facebook")
        {
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            attempt.RemoteId = result.RootElement.GetProperty("id").GetString()!;
            attempt.Url = "https://www.facebook.com/" + Uri.EscapeDataString(attempt.RemoteId);
        }
        else
        {
            attempt.RemoteId = response.Headers.TryGetValues("x-restli-id", out var ids) ? ids.First() : "";
            if (attempt.RemoteId.Length == 0)
            {
                logger?.LogWarning("SocialPublish Attempt {AttemptId} Provider {Provider} Stage MissingPostId HttpStatus {HttpStatus}",
                    attempt.Id, attempt.Provider, (int)response.StatusCode);
                attempt.Status = "Unknown";
                return;
            }
            attempt.Url = "https://www.linkedin.com/feed/update/" + Uri.EscapeDataString(attempt.RemoteId) + "/";
        }
        attempt.Status = "Published";
    }
}
