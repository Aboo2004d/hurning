using System.Globalization;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class ListingModel
{
    public HashSet<OpportunityKind> AllowedDetails { get; set; } = [];

    /// <summary>Kinds whose details are public to anonymous visitors (used for structured data).</summary>
    public HashSet<OpportunityKind> PublicDetailKinds { get; set; } = [];
    public HashSet<long> SavedIds { get; set; } = [];
    public List<Opportunity> Items { get; set; } = [];
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageCount => PublicListing.PageCount(Count);
    public List<FilterOption> Cities { get; set; } = [];
    public string Query { get; set; } = "";
    public OpportunityKind? Kind { get; set; }
    public string City { get; set; } = "";
    public string Category { get; set; } = "";
    public string Employment { get; set; } = "";
    public string Experience { get; set; } = "";
    public bool Archive { get; set; }

    /// <summary>"newest" (default) or "closing".</summary>
    public string Sort { get; set; } = ListingSort.Newest;
    public List<FilterOption> Categories { get; set; } = [];
    public List<FilterOption> EmploymentTypes { get; set; } = [];
    public List<FilterOption> ExperienceLevels { get; set; } = [];
    public List<Opportunity> ClosingSoon { get; set; } = [];
    public DateOnly Today { get; set; } = TextRules.PalestineToday();

    public bool HasFilters =>
        Query.Length > 0
        || City.Length > 0
        || Category.Length > 0
        || Employment.Length > 0
        || Experience.Length > 0;

    /// <summary>Filters that can be turned into member alert preferences.</summary>
    public bool CanAlert => !Archive && (Kind is not null || City.Length > 0 || Category.Length > 0);

    /// <summary>Query values for listing links; empty values, archive=false, sort=newest and page 1 are omitted.</summary>
    public Dictionary<string, string> RouteValues(int page)
    {
        var values = new Dictionary<string, string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[key] = value;
        }
        Add("q", Query);
        Add("kind", Kind?.ToString());
        Add("city", City);
        Add("category", Category);
        if (Kind == OpportunityKind.Job)
        {
            Add("employment", Employment);
            Add("experience", Experience);
        }
        if (Archive)
            values["archive"] = "true";
        if (Sort != ListingSort.Newest)
            values["sort"] = Sort;
        if (page > 1)
            values["page"] = page.ToString(CultureInfo.InvariantCulture);
        return values;
    }
}

public static class ListingSort
{
    public const string Newest = "newest";
    public const string Closing = "closing";

    public static string Parse(string? value) => value == Closing ? Closing : Newest;
}

/// <summary>Indexable landing page for one kind in one city, or for one organization.</summary>
public sealed class BrowseModel : ListingModel
{
    public string Path { get; set; } = "/";
    public string Heading { get; set; } = "";
    public string Intro { get; set; } = "";
    public City? BrowseCity { get; set; }
    public OrganizationFacet? Organization { get; set; }
}

public sealed record PaginationModel(int Page, int PageCount, Func<int, string> Url);

public sealed record CardListModel(ListingModel Listing, IEnumerable<Opportunity> Items, bool Compact = false);
