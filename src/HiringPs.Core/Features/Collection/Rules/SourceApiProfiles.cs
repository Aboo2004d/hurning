using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace HiringPs.Core;

/// <summary>Tested declarative presets, using the same JSON API reader for every source.</summary>
public static class SourceApiProfiles
{
    public static Source? ForWebsite(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.IdnHost != "jobmatch.ps"
            || uri.AbsolutePath.TrimEnd('/') is not ("" or "/jobs")) return null;
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (query.Keys.Any(x => x is not ("country" or "page" or "per_page"))
            || query.Any(x => x.Value.Count != 1)
            || (query.TryGetValue("country", out var country) && country != "1000000055"))
            throw new InvalidOperationException("Jobmatch preset supports the tested Palestine country filter only.");
        var size = 5;
        if (query.TryGetValue("per_page", out var perPage) && (!int.TryParse(perPage, out size) || size is < 1 or > 50))
            throw new InvalidOperationException("API page size must be 1–50.");
        var listing = $"https://jobmatch.ps/jobs?country=1000000055&page=1&per_page={size}";
        var conf = new SourceConfiguration
        {
            AllowedHosts = ["jobmatch.ps", "api.jobmatch.ps"], MaxPages = 50,
            Api = new()
            {
                ListingUrlTemplate = $"https://api.jobmatch.ps/api/v1/jobs?country=1000000055&page={{page}}&per_page={size}",
                ItemUrlTemplate = "https://jobmatch.ps/jobs/{slug}",
                DetailUrlTemplate = "https://api.jobmatch.ps/api/v1/jobs/{slug}",
                Fields = [
                    new("Title", "title"), new("Organization", "organization.name"),
                    new("Country", "location.country.label"), new("Region", "location.region.label"),
                    new("City", "location.city.label"), new("Published at", "published_at"),
                    new("Application deadline", "expires_at"), new("Employment type", "employment_type.title"),
                    new("Work format", "work_format.title"), new("Description", "description", true),
                    new("Requirements", "requirements.education_requirements_text"),
                    new("Skills", "skills"), new("Responsibilities", "responsibilities", true),
                    new("Original announcement URL", "original_url"), new("Application URL", "external_job_link")
                ]
            }
        };
        JsonApiReader.Validate(listing, conf);
        return new() { Name = "Jobmatch — وظائف فلسطين", Url = listing,
            Connector = ConnectorKind.JsonApi, ConfigurationJson = JsonSerializer.Serialize(conf) };
    }
}
