// أسماء المدن بالعربية والإنجليزية وروابطها الثابتة، مع ربطها بأسماء البحث في LocationSearch.
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record City(string Slug, string Arabic, string English)
{
    public string Name(bool english) => english ? English : Arabic;

    /// <summary>Value matched against Opportunity.LocationSearchText (which already contains aliases).</summary>
    public string SearchKey => LocationSearch.Normalize(Arabic);
}

public static class CityCatalog
{
    public static readonly City[] All =
    [
        new("ramallah", "رام الله", "Ramallah"),
        new("al-bireh", "البيرة", "Al-Bireh"),
        new("gaza", "غزة", "Gaza"),
        new("jerusalem", "القدس", "Jerusalem"),
        new("hebron", "الخليل", "Hebron"),
        new("nablus", "نابلس", "Nablus"),
        new("jenin", "جنين", "Jenin"),
        new("bethlehem", "بيت لحم", "Bethlehem"),
        new("jericho", "أريحا", "Jericho"),
        new("tulkarm", "طولكرم", "Tulkarm"),
        new("qalqilya", "قلقيلية", "Qalqilya"),
        new("tubas", "طوباس", "Tubas"),
        new("salfit", "سلفيت", "Salfit"),
    ];

    /// <summary>Arabic names used as stable filter and preference values.</summary>
    public static IEnumerable<string> ArabicNames => All.Select(x => x.Arabic);

    public static City? FromSlug(string? slug) =>
        All.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static City? Find(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = LocationSearch.Normalize(value);
        foreach (var city in All)
        {
            if (
                LocationSearch.Normalize(city.Arabic) == normalized
                || LocationSearch.Normalize(city.English) == normalized
            )
                return city;
            var group = LocationSearch.Groups.FirstOrDefault(g =>
                g.Any(alias => LocationSearch.Normalize(alias) == LocationSearch.Normalize(city.Arabic))
            );
            if (group?.Any(alias => LocationSearch.Normalize(alias) == normalized) == true)
                return city;
        }
        return null;
    }

    /// <summary>Localized display for a filter/preference location value, falling back to the stored text.</summary>
    public static string Display(string value, bool english) => Find(value)?.Name(english) ?? value;
}
