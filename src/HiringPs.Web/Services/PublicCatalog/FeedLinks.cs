using HiringPs.Web.Controllers;
using Microsoft.AspNetCore.WebUtilities;

namespace HiringPs.Web.Models;

/// <summary>Builds a feed URL from the active listing criteria; pagination is intentionally omitted.</summary>
public static class FeedLinks
{
    /// <summary>Encodes the same filters used by the listing in a reusable subscription URL.</summary>
    public static string ForListing(ListingModel model)
    {
        var values = model.RouteValues(1);
        values.Remove("kind");
        var path = model.Kind is { } kind ? FeedsController.Path(kind) : "/feeds/search.xml";
        return QueryHelpers.AddQueryString(path, values.ToDictionary(x => x.Key, x => (string?)x.Value));
    }
}
