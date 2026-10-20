// مسارات الزائر: عرض الفرص وتفاصيلها وصفحات الموقع واللغة وخرائط البحث.
using System.Globalization;
using System.Security.Claims;
using System.Xml.Linq;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace HiringPs.Web.Controllers;

public class HomeController(
    SiteSeo seo,
    MembershipService membership,
    PublicCatalog catalog,
    HiringPs.Web.Services.PublicCatalog.PublicListingReadService listings,
    SavedOpportunityService saved,
    PublicSitemapReadService sitemap
) : Controller
{
    [OutputCache(PolicyName = SiteCache.Listing)]
    public async Task<IActionResult> Index(
        string? q,
        OpportunityKind? kind,
        string? city,
        string? category,
        string? employment,
        string? experience,
        string? sort,
        bool archive = false,
        int page = 1,
        CancellationToken ct = default
    )
    {
        if (kind is not null && !Enum.IsDefined(kind.Value))
            return BadRequest();
        ViewData["ListingKind"] = kind;
        ViewData["ListingArchive"] = archive;
        var english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var today = TextRules.PalestineToday();
        q = q?.Trim().Truncate(200);
        var order = ListingSort.Parse(sort);
        var result = await listings.ReadAsync(new(q, kind, city, category, employment, experience), archive, order, page, today, ct);
        if (result is null)
            return NotFound();
        var filters = result.Filters;
        var allowed = await membership.DetailKindsAsync(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            User.IsInRole("Admin"),
            ct
        );
        var model = new ListingModel
        {
            Items = result.Page.Items,
            AllowedDetails = allowed,
            PublicDetailKinds = await catalog.PublicDetailKindsAsync(ct),
            Count = result.Page.Count,
            Page = page,
            Query = q ?? "",
            Kind = kind,
            City = city ?? "",
            Category = category ?? "",
            Employment = employment ?? "",
            Experience = experience ?? "",
            Archive = archive,
            Sort = order,
            Today = today,
        };
        model.ClosingSoon = result.ClosingSoon;
        await FillSavedAsync(model, ct);
        model.Cities = filters
            .Locations.Select(x => new FilterOption(x, CityCatalog.Display(x, english)))
            .OrderBy(x => x.Label, StringComparer.Create(CultureInfo.CurrentCulture, true))
            .ToList();
        model.Categories = FilterLabels.Options(filters.Categories, FilterLabels.Category, english);
        model.EmploymentTypes = FilterLabels.Options(
            filters.EmploymentTypes,
            FilterLabels.Employment,
            english
        );
        model.ExperienceLevels = FilterLabels.Options(
            filters.ExperienceLevels,
            FilterLabels.Experience,
            english
        );
        ViewBag.Facets = !archive && !model.HasFilters && page == 1 ? await catalog.FacetsAsync(ct) : null;
        return View(model);
    }

    internal async Task FillSavedAsync(ListingModel model, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || User.IsInRole("Admin"))
            return;
        var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ids = model.Items.Select(x => x.Id).ToArray();
        model.SavedIds = await saved.IdsAsync(memberId!, ids, ct);
        Response.Headers.CacheControl = "no-store";
    }

    [HttpGet("llms.txt"), OutputCache(PolicyName = SiteCache.Files)]
    public IActionResult LanguageModelGuide()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("# التوظيف في فلسطين | Employment in Palestine");
        text.AppendLine();
        text.AppendLine(
            "> hiring.ps aggregates jobs, training opportunities, tenders and procurement notices related to Palestine. It is not the recruiting employer and does not receive CVs or tender bids."
        );
        text.AppendLine();
        text.AppendLine("## Public reference pages");
        text.AppendLine(
            $"- [About and how it works (Arabic)]({seo.Url("/About", false)}): identity, sources, application process and limitations."
        );
        text.AppendLine(
            $"- [About and how it works (English)]({seo.Url("/About", true)}): the same public facts in English."
        );
        text.AppendLine($"- [Contact]({seo.Url("/Contact", false)}): contact and corrections.");
        text.AppendLine(
            $"- [Privacy]({seo.Url("/Privacy", false)}): published privacy information."
        );
        text.AppendLine();
        text.AppendLine("## Opportunity categories");
        foreach (var kind in Enum.GetValues<OpportunityKind>())
            text.AppendLine(
                $"- [{kind}]({seo.Url("/", false, [new("kind", kind.ToString())])}): current public listings; follow each detail page to its source. RSS: {seo.Origin}/feeds/{KindSlugs.For(kind)}.xml"
            );
        text.AppendLine(
            $"- [Sitemap index]({seo.Origin}/sitemap.xml): published, non-expired opportunities, city and organization pages, and language variants."
        );
        text.AppendLine();
        text.AppendLine("## Interpreting opportunity records");
        text.AppendLine(
            "Detail pages are summaries, not complete original announcements. Attribute the announcement to its named original source and link to it when citing conditions. Do not attribute employer announcements to hiring.ps as the employer."
        );
        text.AppendLine(
            "Last verification is not the original publication date. A missing deadline does not establish that an opportunity is still open. Archived or expired records are historical, not current offers. Verify application conditions at the source."
        );
        text.AppendLine(
            "Automated collection and extraction may be incomplete or incorrect. This guide is informational and does not override robots.txt, authentication or access restrictions."
        );
        return Content(text.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("robots.txt"), OutputCache(PolicyName = SiteCache.Files)]
    public IActionResult Robots()
    {
        var rules = new System.Text.StringBuilder("User-agent: *\nAllow: /\n");
        foreach (var prefix in new[] { "", "/ar", "/en" })
            foreach (
                var path in new[]
                {
                "/Admin",
                "/Account",
                "/Member",
                "/Registration",
                "/PasswordReset",
                "/Google",
                "/integrations",
                }
            )
                foreach (var variant in new[] { path, path.ToLowerInvariant() }.Distinct())
                    rules.AppendLine("Disallow: " + prefix + variant);
        rules.AppendLine("Sitemap: " + seo.Origin + "/sitemap.xml");
        return Content(rules.ToString(), "text/plain");
    }

    [HttpGet("sitemap.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var count = await sitemap.CountAsync(TextRules.PalestineToday(), ct);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var root = new XElement(
            ns + "sitemapindex",
            new XElement(
                ns + "sitemap",
                new XElement(ns + "loc", seo.Origin + "/sitemaps/pages.xml")
            )
        );
        for (
            var page = 1;
            page <= (count + SiteSeo.SitemapPageSize - 1) / SiteSeo.SitemapPageSize;
            page++
        )
            root.Add(
                new XElement(
                    ns + "sitemap",
                    new XElement(ns + "loc", $"{seo.Origin}/sitemaps/opportunities-{page}.xml")
                )
            );
        return Content(new XDocument(root).ToString(), "application/xml; charset=utf-8");
    }

    [HttpGet("sitemaps/pages.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public async Task<IActionResult> SitemapPages(CancellationToken ct)
    {
        var pages = new List<SitemapEntry>();
        var today = TextRules.PalestineToday();
        var activeKinds = await sitemap.ActiveKindsAsync(today, ct);
        var paths = new List<string> { "/About", "/Contact", "/Privacy" };
        if (activeKinds.Count > 0)
            paths.Insert(0, "/");
        foreach (var path in paths)
            pages.Add(
                new(
                    seo.Url(path, false),
                    seo.Url(path, true),
                    path == "/" ? activeKinds.Max(x => x.Changed) : null
                )
            );
        foreach (var kind in activeKinds.Where(x => Enum.IsDefined(x.Kind)).OrderBy(x => x.Kind))
        {
            var query = new[] { new KeyValuePair<string, string?>("kind", kind.Kind.ToString()) };
            pages.Add(new(seo.Url("/", false, query), seo.Url("/", true, query), kind.Changed));
        }
        var facets = await catalog.FacetsAsync(ct);
        foreach (var facet in facets.Cities)
        {
            var path = BrowsePaths.City(facet.Kind, facet.City);
            pages.Add(new(seo.Url(path, false), seo.Url(path, true), facet.LastModified));
        }
        foreach (var organization in facets.Organizations)
        {
            var path = BrowsePaths.Organization(organization.Slug);
            pages.Add(new(seo.Url(path, false), seo.Url(path, true), organization.LastModified));
        }

        return SitemapDocument(pages);
    }

    [HttpGet("sitemaps/opportunities-{page:int}.xml"), OutputCache(PolicyName = SiteCache.Files)]
    public async Task<IActionResult> SitemapOpportunities(int page, CancellationToken ct)
    {
        if (page < 1 || page > int.MaxValue / SiteSeo.SitemapPageSize)
            return NotFound();
        var items = await sitemap.PageAsync(TextRules.PalestineToday(), page, SiteSeo.SitemapPageSize, ct);
        if (items.Count == 0)
            return NotFound();
        return SitemapDocument(
            items.Select(item =>
            {
                var path = PublicListing.DetailsPath(item.Id, item.Title);
                return new SitemapEntry(seo.Url(path, false), seo.Url(path, true), item.Changed);
            })
        );
    }

    private sealed record SitemapEntry(string Arabic, string English, DateTimeOffset? LastModified);

    private IActionResult SitemapDocument(IEnumerable<SitemapEntry> pages)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        var root = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml));
        foreach (var pair in pages)
            foreach (var url in new[] { pair.Arabic, pair.English })
                root.Add(
                    new XElement(
                        ns + "url",
                        new XElement(ns + "loc", url),
                        pair.LastModified is { } changed
                            ? new XElement(
                                ns + "lastmod",
                                changed.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            )
                            : null,
                        new XElement(
                            xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", "ar"),
                            new XAttribute("href", pair.Arabic)
                        ),
                        new XElement(
                            xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", "en"),
                            new XAttribute("href", pair.English)
                        ),
                        new XElement(
                            xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", "x-default"),
                            new XAttribute("href", pair.Arabic)
                        )
                    )
                );
        return Content(new XDocument(root).ToString(), "application/xml; charset=utf-8");
    }

    public IActionResult Error() =>
        View(
            new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            }
        );
}
