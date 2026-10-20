// خلاصات RSS 2.0 عامة لكل نوع فرصة ولكل نوع في مدينة: آخر 50 فرصة حالية تفاصيلها عامة.
using System.Globalization;
using System.Xml.Linq;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace HiringPs.Web.Controllers;

public class FeedsController(SiteSeo seo, HiringPs.Web.Services.PublicCatalog.PublicListingReadService listings) : Controller
{
    private const int FeedSize = 50;

    [HttpGet("feeds/{kindSlug}.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public Task<IActionResult> Kind(string kindSlug, CancellationToken ct) => FeedAsync(kindSlug, null, ct);

    [HttpGet("feeds/search.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public Task<IActionResult> Search(CancellationToken ct) => FeedAsync(null, null, ct);

    [HttpGet("feeds/{kindSlug}/{citySlug}.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public Task<IActionResult> City(string kindSlug, string citySlug, CancellationToken ct) =>
        FeedAsync(kindSlug, citySlug, ct);

    public static string Path(OpportunityKind kind, City? city = null) =>
        "/feeds/" + KindSlugs.For(kind) + (city is null ? "" : "/" + city.Slug) + ".xml";

    private async Task<IActionResult> FeedAsync(string? kindSlug, string? citySlug, CancellationToken ct)
    {
        var kind = kindSlug is null ? null : KindSlugs.Parse(kindSlug);
        var city = citySlug is null ? null : CityCatalog.FromSlug(citySlug);
        if ((kindSlug is not null && kind is null) || (citySlug is not null && city is null))
            return NotFound();
        var archive = string.Equals(Request.Query["archive"], "true", StringComparison.OrdinalIgnoreCase);
        var q = Request.Query["q"].ToString();
        var selectedCity = city?.SearchKey ?? Request.Query["city"].ToString();
        var category = Request.Query["category"].ToString();
        var employment = Request.Query["employment"].ToString();
        var experience = Request.Query["experience"].ToString();
        var items = await listings.FeedAsync(new(q, kind, selectedCity, category, employment, experience),
            archive, ListingSort.Parse(Request.Query["sort"].ToString()), TextRules.PalestineToday(), FeedSize, ct);
        var (arabicKind, englishKind) = kind switch
        {
            OpportunityKind.Job => ("وظائف", "Jobs"),
            OpportunityKind.Training => ("فرص تدريب", "Training"),
            OpportunityKind.Tender => ("عطاءات", "Tenders"),
            OpportunityKind.ProcurementNotice => ("مناقصات", "Procurement notices"),
            _ => ("فرص", "Opportunities"),
        };
        var place = city is null ? ("فلسطين", "Palestine") : (city.Arabic, city.English);
        var listing = new ListingModel
        {
            Query = q,
            Kind = kind,
            City = selectedCity,
            Category = category,
            Employment = employment,
            Experience = experience,
            Archive = archive,
            Sort = ListingSort.Parse(Request.Query["sort"].ToString())
        };
        var channelLink = city is not null && Request.Query.Count == 0
            ? seo.Url(BrowsePaths.City(kind!.Value, city), false)
            : seo.Url("/", false, listing.RouteValues(1).Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var channel = new XElement(
            "channel",
            new XElement("title", $"{arabicKind} في {place.Item1} | {englishKind} in {place.Item2} — hiring.ps"),
            new XElement("link", channelLink),
            new XElement(
                "description",
                $"أحدث {arabicKind} المنشورة في {place.Item1} على التوظيف في فلسطين. Latest {englishKind.ToLowerInvariant()} in {place.Item2}."
            ),
            new XElement("language", "ar"),
            new XElement(
                atom + "link",
                new XAttribute("href", seo.Origin + (city is not null && Request.Query.Count == 0 ? Path(kind!.Value, city) : FeedLinks.ForListing(listing))),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/rss+xml")
            )
        );
        if (items.Count > 0)
            channel.Add(new XElement("lastBuildDate", Rfc822(items.Max(PublicListing.ContentDate))));
        foreach (var item in items)
        {
            var link = seo.Url(PublicListing.DetailsPath(item), false);
            var summary = string.Join(
                " — ",
                new[]
                {
                    item.Organization,
                    item.Locations,
                    item.Summary,
                    item.Deadline is { } deadline
                        ? "آخر موعد / Deadline: " + deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : "",
                }.Where(x => !string.IsNullOrWhiteSpace(x))
            );
            channel.Add(
                new XElement(
                    "item",
                    new XElement("title", item.Title),
                    new XElement("link", link),
                    new XElement("guid", new XAttribute("isPermaLink", "true"), link),
                    new XElement("description", SiteSeo.Snippet(summary, 500)),
                    new XElement("pubDate", Rfc822(item.SourcePublishedAt ?? item.CreatedAt))
                )
            );
        }
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "atom", atom), channel)
        );
        return Content(document.Declaration + "\n" + document.Root, "application/rss+xml; charset=utf-8");
    }

    private static string Rfc822(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
}
