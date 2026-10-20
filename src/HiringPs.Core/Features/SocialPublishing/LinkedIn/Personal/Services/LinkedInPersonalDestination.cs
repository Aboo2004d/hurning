using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>هوية الحساب الشخصي؛ تستخدم userinfo وصلاحية w_member_social بدل صلاحيات المؤسسات.</summary>
public sealed class LinkedInPersonalDestination(SocialProviderTransport transport) : ILinkedInDestination
{
    /// <inheritdoc />
    public string Key => "LinkedInPersonal";
    /// <inheritdoc />
    public string PublishingScope => "w_member_social";
    /// <inheritdoc />
    public string AuthorPrefix => "urn:li:person:";

    /// <summary>يقرأ هوية صاحب الرمز ويطابقها بالربط المحفوظ عند تمرير معرّف متوقع.</summary>
    public async Task<SocialIdentityDto> ResolveAsync(string expectedId, string token, CancellationToken ct)
    {
        using var request = transport.Request("https://api.linkedin.com/v2/userinfo", token);
        using var response = await transport.ReadAsync(request, ct);
        var profile = response.RootElement;
        var id = profile.GetProperty("sub").GetString() ?? "";
        if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,100}$") || (expectedId.Length > 0 && expectedId != id))
            throw new InvalidOperationException("IntegrationPageInvalid");
        return new(id, profile.TryGetProperty("name", out var name) ? name.GetString() ?? id : id);
    }
}
