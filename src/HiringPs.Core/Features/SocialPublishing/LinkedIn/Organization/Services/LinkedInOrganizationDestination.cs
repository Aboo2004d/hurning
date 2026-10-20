namespace HiringPs.Core;

/// <summary>هوية المؤسسة؛ تتحقق من صلاحيات إدارة الصفحة قبل إتاحة النشر باسمها.</summary>
public sealed class LinkedInOrganizationDestination(SocialProviderTransport transport) : ILinkedInDestination
{
    /// <inheritdoc />
    public string Key => "LinkedIn";
    /// <inheritdoc />
    public string PublishingScope => "w_organization_social";
    /// <inheritdoc />
    public string AuthorPrefix => "urn:li:organization:";

    /// <summary>يتحقق من دور المستخدم على المؤسسة المحددة ويقرأ اسمها من LinkedIn.</summary>
    public async Task<SocialIdentityDto> ResolveAsync(string expectedId, string token, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(expectedId, "^[0-9]{1,40}$"))
            throw new InvalidOperationException("IntegrationPageInvalid");
        using var aclRequest = transport.Request("https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&state=APPROVED", token);
        using var acl = await transport.ReadAsync(aclRequest, ct);
        var urn = AuthorPrefix + expectedId;
        if (!acl.RootElement.GetProperty("elements").EnumerateArray().Any(x => x.GetProperty("organization").GetString() == urn
            && x.GetProperty("role").GetString() is "ADMINISTRATOR" or "CONTENT_ADMINISTRATOR"))
            throw new InvalidOperationException("IntegrationPermissionMissing");
        using var orgRequest = transport.Request("https://api.linkedin.com/rest/organizations/" + expectedId, token);
        using var org = await transport.ReadAsync(orgRequest, ct);
        return new(expectedId, org.RootElement.GetProperty("localizedName").GetString() ?? expectedId);
    }
}
