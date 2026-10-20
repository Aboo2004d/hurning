using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>عميل LinkedIn: صلاحيات المؤسسة والحساب الشخصي وهوياتهما وإرسال Posts API.</summary>
public sealed class LinkedInPublishingClient(SocialProviderTransport transport,
    LinkedInPersonalDestination? personal = null, LinkedInOrganizationDestination? organization = null)
{
    private readonly LinkedInPersonalDestination personal = personal ?? new(transport);
    private readonly LinkedInOrganizationDestination organization = organization ?? new(transport);

    /// <summary>يختار سياسة الهوية والصلاحية الخاصة بحساب LinkedIn الشخصي أو المؤسسة.</summary>
    private ILinkedInDestination Destination(string provider) => provider switch
    {
        "LinkedInPersonal" => personal,
        "LinkedIn" => organization,
        _ => throw new InvalidOperationException("IntegrationNotConfigured")
    };

    /// <summary>يقرأ صفحة المؤسسة عبر خدمة هويتها المستقلة دون تغيير رمز التفويض.</summary>
    public async Task<(string Token, string Name)> ResolvePageAsync(string pageId, string token, CancellationToken ct)
    {
        var identity = await organization.ResolveAsync(pageId, token, ct);
        return (token, identity.Name);
    }

    /// <summary>يقرأ هوية الحساب الشخصي عبر خدمة مستقلة عن إدارة المؤسسة.</summary>
    public async Task<(string Id, string Name)> ResolvePersonalAsync(string token, CancellationToken ct)
    {
        var identity = await personal.ResolveAsync("", token, ct);
        return (identity.Id, identity.Name);
    }
    /// <summary>يتحقق من صلاحية رمز LinkedIn ونطاق النشر الخاص بوجهته.</summary>
    public async Task ValidateLinkedInGrantAsync(string provider, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.linkedin.com/oauth/v2/introspectToken")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token, ["client_id"] = transport.RequiredLinkedIn(provider, "ClientId"),
                ["client_secret"] = transport.RequiredLinkedIn(provider, "ClientSecret")
            })
        };
        using var grant = await transport.ReadAsync(request, ct);
        var permission = Destination(provider).PublishingScope;
        if (!grant.RootElement.GetProperty("active").GetBoolean()
            || !grant.RootElement.GetProperty("scope").GetString()!.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Contains(permission))
            throw new InvalidOperationException("IntegrationPermissionMissing");
    }

    /// <summary>يتحقق من هوية الوجهة المحددة وصلاحية النشر المناسبة لها دون إنشاء منشور.</summary>
    public async Task ProbeAsync(string provider, string authorId, string token, CancellationToken ct)
    {
        await Destination(provider).ResolveAsync(authorId, token, ct);
        await ValidateLinkedInGrantAsync(provider, token, ct);
    }
    /// <summary>يرسل منشورًا إلى الحساب الشخصي أو المؤسسة حسب الوجهة دون خلط رموزهما.</summary>
    public async Task<SocialPublishResultDto> PublishAsync(SocialPublishRequestDto publication, string token, CancellationToken ct)
    {
        using var request = transport.Request("https://api.linkedin.com/rest/posts", token, publication.Provider);
        request.Method = HttpMethod.Post;
        request.Content = JsonContent.Create(new { author = Destination(publication.Provider).AuthorPrefix + publication.AuthorId, commentary = publication.Text,
            visibility = "PUBLIC", distribution = new { feedDistribution = "MAIN_FEED", targetEntities = Array.Empty<object>(), thirdPartyDistributionChannels = Array.Empty<object>() }, lifecycleState = "PUBLISHED", isReshareDisabledByAuthor = false });
        using var response = await transport.SendPublicationAsync(request, publication, ct);
        if (!response.IsSuccessStatusCode) return SocialProviderTransport.Rejected(response);
        var id = response.Headers.TryGetValues("x-restli-id", out var ids) ? ids.First() : "";
        if (id.Length == 0)
        {
            transport.Logger?.LogWarning("SocialPublish Attempt {AttemptId} Provider {Provider} Stage MissingPostId HttpStatus {HttpStatus}",
                publication.AttemptId, publication.Provider, (int)response.StatusCode);
            return new(SocialPublicationOutcome.Unknown);
        }
        return new(SocialPublicationOutcome.Published, id, "https://www.linkedin.com/feed/update/" + Uri.EscapeDataString(id) + "/");
    }
}
