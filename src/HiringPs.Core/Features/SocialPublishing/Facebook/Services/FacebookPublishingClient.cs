using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>عميل فيسبوك: اختيار الصفحة والتحقق من تفويضها وإرسال المنشور عبر Graph API.</summary>
public sealed class FacebookPublishingClient(SocialProviderTransport transport)
{
    /// <summary>يتحقق من الصفحة وصلاحيات إدارتها ويعيد هويتها ورمزها دون نشر محتوى.</summary>
    public async Task<(string Token, string Name)> ResolvePageAsync(string pageId, string token, CancellationToken ct)
    {
        using var request = transport.Request(transport.FacebookRoot + "/me/accounts?fields=id,name,access_token,tasks&limit=100", token);
        using var pages = await transport.ReadAsync(request, ct);
        foreach (var page in pages.RootElement.GetProperty("data").EnumerateArray())
        {
            if (page.GetProperty("id").GetString() != pageId) continue;
            if (!page.GetProperty("tasks").EnumerateArray().Any(x => x.GetString() is "CREATE_CONTENT" or "MANAGE" or "PROFILE_PLUS_CREATE_CONTENT")) throw new InvalidOperationException("IntegrationPermissionMissing");
            return (page.GetProperty("access_token").GetString()!, page.GetProperty("name").GetString()!);
        }
        // Some business-managed pages are omitted from /me/accounts despite a page grant.
        // Resolve only the explicitly selected page, then validate its own token.
        transport.Logger?.LogInformation("SocialConnection Provider Facebook Stage DirectPageLookup PageId {PageId}", pageId);
        using var directRequest = transport.Request(transport.FacebookRoot + "/" + pageId + "?fields=id,name,access_token", token);
        using var direct = await transport.ReadAsync(directRequest, ct);
        var selected = direct.RootElement;
        if (selected.GetProperty("id").GetString() != pageId)
            throw new InvalidOperationException("IntegrationPageInvalid");
        var pageToken = selected.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(pageToken))
            throw new InvalidOperationException("IntegrationPageUnavailable");
        await ValidatePageTokenAsync(pageId, pageToken, ct);
        return (pageToken, selected.GetProperty("name").GetString() ?? pageId);
    }
    /// <summary>يتحقق من أن رمز فيسبوك يعود للصفحة المطلوبة ويحمل صلاحية النشر عليها.</summary>
    public async Task ValidatePageTokenAsync(string pageId, string token, CancellationToken ct)
    {
        using var identityRequest = transport.Request(transport.FacebookRoot + "/me?fields=id", token);
        using var identity = await transport.ReadAsync(identityRequest, ct);
        if (identity.RootElement.GetProperty("id").GetString() != pageId)
            throw new InvalidOperationException("IntegrationPageInvalid");
        transport.Logger?.LogInformation("SocialConnection Provider Facebook Stage ValidatePageGrant PageId {PageId}", pageId);
        using var request = transport.Request(transport.FacebookRoot + "/debug_token?input_token=" + Uri.EscapeDataString(token),
            transport.Required("Integrations:Facebook:ClientId") + "|" + transport.Required("Integrations:Facebook:ClientSecret"));
        using var response = await transport.ReadAsync(request, ct);
        var grant = response.RootElement.GetProperty("data");
        if (!grant.GetProperty("is_valid").GetBoolean()
            || grant.GetProperty("app_id").GetString() != transport.Required("Integrations:Facebook:ClientId")
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

    /// <summary>يفحص هوية الصفحة وصلاحية pages_manage_posts دون إنشاء محتوى.</summary>
    public async Task ProbeAsync(string pageId, string token, CancellationToken ct)
    {
        using var request = transport.Request(transport.FacebookRoot + "/" + pageId + "?fields=id,name", token);
        using var page = await transport.ReadAsync(request, ct);
        if (page.RootElement.GetProperty("id").GetString() != pageId) throw new InvalidOperationException("IntegrationPageInvalid");
        using var debug = transport.Request(transport.FacebookRoot + "/debug_token?input_token=" + Uri.EscapeDataString(token), transport.Required("Integrations:Facebook:ClientId") + "|" + transport.Required("Integrations:Facebook:ClientSecret"));
        using var grant = await transport.ReadAsync(debug, ct);
        var data = grant.RootElement.GetProperty("data");
        if (!data.GetProperty("is_valid").GetBoolean()
            || data.GetProperty("app_id").GetString() != transport.Required("Integrations:Facebook:ClientId")
            || !data.GetProperty("scopes").EnumerateArray().Any(x => x.GetString() == "pages_manage_posts")) throw new InvalidOperationException("IntegrationPermissionMissing");
    }
    /// <summary>ينشر نصًا واحدًا باسم الصفحة ويعيد النتيجة دون حفظها في القاعدة.</summary>
    public async Task<SocialPublishResultDto> PublishAsync(SocialPublishRequestDto publication, string token, CancellationToken ct)
    {
        using var request = transport.Request(transport.FacebookRoot + "/" + publication.AuthorId + "/feed", token);
        request.Method = HttpMethod.Post;
        request.Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["message"] = publication.Text });
        using var response = await transport.SendPublicationAsync(request, publication, ct);
        if (!response.IsSuccessStatusCode) return SocialProviderTransport.Rejected(response);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var id = result.RootElement.GetProperty("id").GetString()!;
        return new(SocialPublicationOutcome.Published, id, "https://www.facebook.com/" + Uri.EscapeDataString(id));
    }
}
