// مسارات الزائر: عرض الفرص وتفاصيلها وصفحات الموقع واللغة وخرائط البحث.
using System.Security.Claims;
using System.Xml.Linq;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class HomeController(HiringDb db, SiteSeo seo, MembershipService membership) : Controller
{
    public async Task<IActionResult> Index(
        string? q,
        OpportunityKind? kind,
        string? city,
        string? category,
        string? employment,
        string? experience,
        bool archive = false,
        int page = 1,
        CancellationToken ct = default
    )
    {
        if (kind is not null && !Enum.IsDefined(kind.Value))
            return BadRequest();
        ViewData["ListingKind"] = kind;
        ViewData["ListingArchive"] = archive;
        var today = TextRules.PalestineToday();
        var query = db
            .Opportunities.AsNoTracking()
            .Where(x =>
                archive
                    ? (
                        x.Status == PublicationStatus.Archived
                        || (
                            x.Status == PublicationStatus.Published
                            && x.Deadline != null
                            && x.Deadline < today
                        )
                    )
                    : (
                        x.Status == PublicationStatus.Published
                        && (x.Deadline == null || x.Deadline >= today)
                    )
            );
        var scoped = kind is null ? query : query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().Truncate(200);
            foreach (
                var term in LocationSearch
                    .Normalize(q)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct()
                    .Take(12)
            )
                query = query.Where(x => x.SearchText.Contains(term));
        }

        if (kind is not null)
            query = query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = LocationSearch.Normalize(city);
            query = query.Where(x => x.LocationSearchText.Contains(normalizedCity));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);
        if (kind == OpportunityKind.Job)
        {
            if (!string.IsNullOrWhiteSpace(employment))
                query = query.Where(x => x.EmploymentType == employment);
            if (!string.IsNullOrWhiteSpace(experience))
                query = query.Where(x => x.ExperienceLevel == experience);
        }

        var count = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (count + 11) / 12));
        var allowed = await membership.DetailKindsAsync(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.IsInRole("Admin"),
            ct
        );
        var model = new ListingModel
        {
            Items = await query
                .OrderByDescending(x => x.SourcePublishedAt ?? x.CreatedAt)
                .Skip((page - 1) * 12)
                .Take(12)
                .ToListAsync(ct),
            AllowedDetails = allowed,
            Count = count,
            Page = page,
            Query = q ?? "",
            Kind = kind,
            City = city ?? "",
            Category = category ?? "",
            Employment = employment ?? "",
            Experience = experience ?? "",
            Archive = archive,
        };
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ids = model.Items.Select(x => x.Id).ToArray();
            model.SavedIds = (
                await db
                    .SavedOpportunities.AsNoTracking()
                    .Where(x => x.UserId == memberId && ids.Contains(x.OpportunityId))
                    .Select(x => x.OpportunityId)
                    .ToListAsync(ct)
            ).ToHashSet();
            Response.Headers.CacheControl = "no-store";
        }
        var published = scoped;
        model.Cities = new[]
        {
            "رام الله",
            "غزة",
            "القدس",
            "الخليل",
            "نابلس",
            "جنين",
            "بيت لحم",
            "أريحا",
            "طولكرم",
            "قلقيلية",
            "طوباس",
            "سلفيت",
        }
            .Concat(await LocationCatalog.ApprovedAsync(db, ct))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        model.Categories = await published
            .Where(x => x.Category != "")
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
        model.EmploymentTypes = await published
            .Where(x => x.Kind == OpportunityKind.Job && x.EmploymentType != "")
            .Select(x => x.EmploymentType)
            .Distinct()
            .ToListAsync(ct);
        model.ExperienceLevels = await published
            .Where(x => x.Kind == OpportunityKind.Job && x.ExperienceLevel != "")
            .Select(x => x.ExperienceLevel)
            .Distinct()
            .ToListAsync(ct);
        return View(model);
    }

    [HttpGet("llms.txt")]
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
                $"- [{kind}]({seo.Url("/", false, [new("kind", kind.ToString())])}): current public listings; follow each detail page to its source."
            );
        text.AppendLine(
            $"- [Sitemap index]({seo.Origin}/sitemap.xml): published, non-expired opportunities and language variants."
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

    [HttpGet("robots.txt")]
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
                "/integrations",
            }
        )
            rules.AppendLine("Disallow: " + prefix + path);
        rules.AppendLine("Sitemap: " + seo.Origin + "/sitemap.xml");
        return Content(rules.ToString(), "text/plain");
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var today = TextRules.PalestineToday();
        var count = await db
            .Opportunities.AsNoTracking()
            .CountAsync(
                x =>
                    x.Status == PublicationStatus.Published
                    && (x.Deadline == null || x.Deadline >= today),
                ct
            );
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

    [HttpGet("sitemaps/pages.xml")]
    public async Task<IActionResult> SitemapPages(CancellationToken ct)
    {
        var pages = new List<(string Arabic, string English)>();
        var today = TextRules.PalestineToday();
        var activeKinds = await db
            .Opportunities.AsNoTracking()
            .Where(x =>
                x.Status == PublicationStatus.Published
                && (x.Deadline == null || x.Deadline >= today)
            )
            .Select(x => x.Kind)
            .Distinct()
            .ToListAsync(ct);
        var paths = new List<string> { "/About", "/Contact", "/Privacy" };
        if (activeKinds.Count > 0)
            paths.Insert(0, "/");
        foreach (var path in paths)
            pages.Add((seo.Url(path, false), seo.Url(path, true)));
        foreach (var kind in activeKinds.Where(kind => Enum.IsDefined(kind)).OrderBy(kind => kind))
        {
            var query = new[] { new KeyValuePair<string, string?>("kind", kind.ToString()) };
            pages.Add((seo.Url("/", false, query), seo.Url("/", true, query)));
        }

        return SitemapDocument(pages);
    }

    [HttpGet("sitemaps/opportunities-{page:int}.xml")]
    public async Task<IActionResult> SitemapOpportunities(int page, CancellationToken ct)
    {
        if (page < 1 || page > int.MaxValue / SiteSeo.SitemapPageSize)
            return NotFound();
        var today = TextRules.PalestineToday();
        var ids = await db
            .Opportunities.AsNoTracking()
            .Where(x =>
                x.Status == PublicationStatus.Published
                && db.OpportunityAccessRules.Any(rule => rule.Kind == x.Kind && rule.PublicDetails)
                && (x.Deadline == null || x.Deadline >= today)
            )
            .OrderBy(x => x.Id)
            .Skip((page - 1) * SiteSeo.SitemapPageSize)
            .Take(SiteSeo.SitemapPageSize)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return NotFound();
        return SitemapDocument(
            ids.Select(id =>
                (
                    seo.Url("/Opportunities/Details/" + id, false),
                    seo.Url("/Opportunities/Details/" + id, true)
                )
            )
        );
    }

    private IActionResult SitemapDocument(IEnumerable<(string Arabic, string English)> pages)
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
