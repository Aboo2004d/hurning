using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace HiringPs.Core;

/// <summary>Tested declarative presets, using the same JSON API reader for every source.</summary>
public static class SourceApiProfiles
{
    public static Source? ForWebsite(string url)
    {
        var foras = Foras(url);
        if (foras is not null) return foras;
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
    private static Source? Foras(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.IdnHost != "foras.ps"
            || uri.AbsolutePath.TrimEnd('/') is not ("" or "/home" or "/opportunities")) return null;
        if (uri.Query.Length > 0)
            throw new InvalidOperationException("Foras all-opportunities preset requires the unfiltered listing URL.");
        const string listing = "https://foras.ps/opportunities";
        var conf = new SourceConfiguration
        {
            AllowedHosts = ["foras.ps"], MaxPages = 50,
            Api = new()
            {
                ListingMethod = "POST",
                ListingUrlTemplate = "https://foras.ps/api/v1/Opportunities/filteredJobs",
                ListingBodyTemplate = """{"pageNumber":{page},"pageSize":10,"orderBy":"date","orderByDirection":"Desc"}""",
                ItemUrlTemplate = "https://foras.ps/opportunities/job-details/{id}",
                DetailUrlTemplate = "https://foras.ps/api/v1/Opportunities/{id}",
                SuccessPath = "", ItemsPath = "result", DetailPath = "", SlugPath = "id",
                CurrentPagePath = "", LastPagePath = "", TotalCountPath = "totalRecords", PageSize = 10,
                ContentPath = "info[language=ar].description", ContentFallbackPaths = ["info[language=en].description"],
                Fields = [
                    new("Title (Arabic)", "info[language=ar].name"),
                    new("Title (English)", "info[language=en].name"),
                    new("Publishing organization (Arabic)", "companyInfo[language=ar].name"),
                    new("Publishing organization (English)", "companyInfo[language=en].name"),
                    new("Opportunity category (Arabic)", "categoryInfo[language=ar].name"),
                    new("Opportunity category (English)", "categoryInfo[language=en].name"),
                    new("Location (Arabic)", "cityInfo[language=ar].name"),
                    new("Location (English)", "cityInfo[language=en].name"),
                    new("Application deadline", "endDate"),
                    new("Description (Arabic)", "info[language=ar].description", true),
                    new("Description (English)", "info[language=en].description", true),
                    new("Benefits (Arabic)", "info[language=ar].benifits", true),
                    new("Benefits (English)", "info[language=en].benifits", true),
                    new("Application process (Arabic)", "info[language=ar].applicationProcess", true),
                    new("Application process (English)", "info[language=en].applicationProcess", true),
                    new("Application URL", "applyUrl")
                ]
            }
        };
        JsonApiReader.Validate(listing, conf);
        return new() { Name = "Foras — جميع الفرص", Url = listing, Connector = ConnectorKind.JsonApi,
            ConfigurationJson = JsonSerializer.Serialize(conf) };
    }

}
