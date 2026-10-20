using HiringPs.Core;
using HiringPs.Web.Models;

namespace HiringPs.Web.Controllers;

/// <summary>Canonical paths for public city and organization browsing.</summary>
public static class BrowsePaths
{
    /// <summary>Returns the canonical opportunity-kind and city listing path.</summary>
    public static string City(OpportunityKind kind, City city) =>
        "/" + KindSlugs.For(kind) + "/city/" + city.Slug;

    /// <summary>Encodes an organization slug in its public listing path.</summary>
    public static string Organization(string slug) => "/org/" + Uri.EscapeDataString(slug);
}
