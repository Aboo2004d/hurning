// مسارات الزائر: عرض الفرص وتفاصيلها وصفحات الموقع واللغة وخرائط البحث.
using System.Xml.Linq;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class HomeController(HiringDb db, SiteSeo seo) : Controller
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
        var model = new ListingModel
        {
            Items = await query
                .OrderByDescending(x => x.SourcePublishedAt ?? x.CreatedAt)
                .Skip((page - 1) * 12)
                .Take(12)
                .ToListAsync(ct),
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

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var item = await db
            .Opportunities.AsNoTracking()
            .Include(x => x.SourceLinks)
                .ThenInclude(x => x.SourceItem)
                    .ThenInclude(x => x.Source)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == id
                    && (
                        x.Status == PublicationStatus.Published
                        || x.Status == PublicationStatus.Archived
                    ),
                ct
            );
        if (item is null)
            return NotFound();
        ViewData["ListingKind"] = item.Kind;
        ViewBag.VerifiedDates = await db
            .OpportunityDates.AsNoTracking()
            .Where(x => x.OpportunityId == item.Id)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return View(item);
    }

    [HttpGet("llms.txt")]
    public IActionResult LanguageModelGuide()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("# فرص فلسطين | Palestine Opportunities");
        text.AppendLine();
        text.AppendLine(
            "> hiring.ps aggregates jobs, training opportunities, tenders and procurement notices related to Palestine. It is not the recruiting employer and does not receive CVs or tender bids."
        );
        text.AppendLine();
        text.AppendLine("## Public reference pages");
        text.AppendLine(
            $"- [About and how it works (Arabic)]({seo.Url("/Home/About", false)}): identity, sources, application process and limitations."
        );
        text.AppendLine(
            $"- [About and how it works (English)]({seo.Url("/Home/About", true)}): the same public facts in English."
        );
        text.AppendLine(
            $"- [Contact]({seo.Url("/Home/Contact", false)}): contact and corrections."
        );
        text.AppendLine(
            $"- [Privacy]({seo.Url("/Home/Privacy", false)}): published privacy information."
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
    public IActionResult Robots() =>
        Content(
            $"User-agent: *\nAllow: /\nDisallow: /Admin\nDisallow: /Account\nSitemap: {seo.Origin}/sitemap.xml\n",
            "text/plain"
        );

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
        var paths = new List<string> { "/Home/About", "/Home/Contact", "/Home/Privacy" };
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
                (seo.Url("/Home/Details/" + id, false), seo.Url("/Home/Details/" + id, true))
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

    [HttpPost, EnableRateLimiting("public")]
    public async Task<IActionResult> Report(
        long id,
        string reason,
        string? details,
        CancellationToken ct
    )
    {
        if (
            reason is not ("expired" or "duplicate" or "incorrect" or "broken" or "other")
            || (details?.Length ?? 0) > 1000
        )
            return BadRequest();
        if (
            !await db.Opportunities.AnyAsync(
                x =>
                    x.Id == id
                    && (
                        x.Status == PublicationStatus.Published
                        || x.Status == PublicationStatus.Archived
                    ),
                ct
            )
        )
            return NotFound();
        db.Reports.Add(
            new()
            {
                OpportunityId = id,
                Reason = reason,
                Details = details ?? "",
            }
        );
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "ReportReceived";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult Language(string culture, string? returnUrl)
    {
        if (culture is not ("ar" or "en"))
            return BadRequest();
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new(culture)),
            new()
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
            }
        );
        var destination = Url.IsLocalUrl(returnUrl) ? Url.Content(returnUrl!) : "/";
        var uri = new Uri(new Uri("https://local.invalid"), destination);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        query.Remove("ui-culture");
        query["culture"] = culture;
        return LocalRedirect(
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(uri.AbsolutePath, query)
                + uri.Fragment
        );
    }

    public async Task<IActionResult> About(CancellationToken ct) =>
        View(await ReadSitePageAsync("about", ct));

    [HttpGet]
    public IActionResult Contact() => View(new ContactInput());

    [HttpPost, EnableRateLimiting("contact")]
    public async Task<IActionResult> Contact(ContactInput input, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Website))
        {
            TempData["Notice"] = "ContactReceived";
            return RedirectToAction(nameof(Contact));
        }

        input.Name = input.Name?.Trim() ?? "";
        input.Email = input.Email?.Trim() ?? "";
        input.Message = input.Message?.Trim() ?? "";
        if (
            input.Email.Any(char.IsControl)
            || !System.Net.Mail.MailAddress.TryCreate(input.Email, out var address)
            || !string.Equals(address.Address, input.Email, StringComparison.OrdinalIgnoreCase)
        )
            ModelState.AddModelError(nameof(input.Email), "ContactEmailInvalid");
        if (!ModelState.IsValid)
            return View(input);
        db.ContactMessages.Add(
            new ContactMessage
            {
                Name = input.Name,
                Email = input.Email,
                Message = input.Message,
            }
        );
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "ContactReceived";
        return RedirectToAction(nameof(Contact));
    }

    public async Task<IActionResult> Privacy(CancellationToken ct) =>
        View(await ReadSitePageAsync("privacy", ct));

    private async Task<SitePageViewModel> ReadSitePageAsync(string key, CancellationToken ct)
    {
        var page = await db.SitePages.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        var language =
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? "en"
                : "ar";
        return SitePageDefaults.Resolve(key, page, language);
    }

    public IActionResult Error() =>
        View(
            new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            }
        );
}
