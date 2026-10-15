using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
namespace HiringPs.Web.Controllers;

public class HomeController(HiringDb db) : Controller
{
    public async Task<IActionResult> Index(string? q, OpportunityKind? kind, string? city, string? category, string? employment, string? experience, bool archive = false, int page = 1, CancellationToken ct = default)
    {
        if (kind is not null && !Enum.IsDefined(kind.Value)) return BadRequest();
        ViewData["ListingKind"] = kind; ViewData["ListingArchive"] = archive;
        var today = TextRules.PalestineToday(); var query = db.Opportunities.AsNoTracking().Where(x => archive ? (x.Status == PublicationStatus.Archived || (x.Status == PublicationStatus.Published && x.Deadline != null && x.Deadline < today)) : (x.Status == PublicationStatus.Published && (x.Deadline == null || x.Deadline >= today)));
        var scoped = kind is null ? query : query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim().Truncate(200); query = query.Where(x => x.Title.Contains(q) || x.Organization.Contains(q) || x.Summary.Contains(q)); }
        if (kind is not null) query = query.Where(x => x.Kind == kind); if (!string.IsNullOrWhiteSpace(city)) query = query.Where(x => x.Locations.Contains(city)); if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (kind == OpportunityKind.Job) { if (!string.IsNullOrWhiteSpace(employment)) query = query.Where(x => x.EmploymentType == employment); if (!string.IsNullOrWhiteSpace(experience)) query = query.Where(x => x.ExperienceLevel == experience); }
        var count = await query.CountAsync(ct); page = Math.Clamp(page, 1, Math.Max(1, (count + 11) / 12));
        var model = new ListingModel { Items = await query.OrderByDescending(x => x.SourcePublishedAt ?? x.CreatedAt).Skip((page - 1) * 12).Take(12).ToListAsync(ct), Count = count, Page = page, Query = q ?? "", Kind = kind, City = city ?? "", Category = category ?? "", Employment = employment ?? "", Experience = experience ?? "", Archive = archive };
        var published = scoped;
        model.Cities = new[] { "رام الله", "غزة", "القدس", "الخليل", "نابلس", "جنين", "بيت لحم", "أريحا", "طولكرم", "قلقيلية", "طوباس", "سلفيت" }.Concat(await LocationCatalog.ApprovedAsync(db, ct)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        model.Categories = await published.Where(x => x.Category != "").Select(x => x.Category).Distinct().OrderBy(x => x).ToListAsync(ct); model.EmploymentTypes = await published.Where(x => x.Kind == OpportunityKind.Job && x.EmploymentType != "").Select(x => x.EmploymentType).Distinct().ToListAsync(ct); model.ExperienceLevels = await published.Where(x => x.Kind == OpportunityKind.Job && x.ExperienceLevel != "").Select(x => x.ExperienceLevel).Distinct().ToListAsync(ct); return View(model);
    }
    public async Task<IActionResult> Details(long id, CancellationToken ct) { var item = await db.Opportunities.AsNoTracking().Include(x => x.SourceLinks).ThenInclude(x => x.SourceItem).ThenInclude(x => x.Source).FirstOrDefaultAsync(x => x.Id == id && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived), ct); if (item is null) return NotFound(); ViewData["ListingKind"] = item.Kind; return View(item); }
    [HttpGet("robots.txt")]
    public IActionResult Robots()=>Content("User-agent: *\nDisallow: /Admin\nDisallow: /Account\nSitemap: https://hiring.ps/sitemap.xml\n","text/plain");
    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        XNamespace ns="http://www.sitemaps.org/schemas/sitemap/0.9";
        var today=TextRules.PalestineToday();var items=await db.Opportunities.AsNoTracking().Where(x=>x.Status==PublicationStatus.Published&&(x.Deadline==null||x.Deadline>=today)).OrderByDescending(x=>x.Id).Select(x=>new{x.Id,x.LastVerifiedAt}).Take(49000).ToListAsync(ct);
        var xml=new XDocument(new XElement(ns+"urlset",new XElement(ns+"url",new XElement(ns+"loc","https://hiring.ps/")),items.Select(x=>new XElement(ns+"url",new XElement(ns+"loc",$"https://hiring.ps/Home/Details/{x.Id}"),new XElement(ns+"lastmod",x.LastVerifiedAt.ToString("yyyy-MM-dd"))))));
        return Content(xml.ToString(),"application/xml");
    }
    [HttpPost, EnableRateLimiting("public")] public async Task<IActionResult> Report(long id, string reason, string? details, CancellationToken ct) { if (reason is not ("expired" or "duplicate" or "incorrect" or "broken" or "other") || (details?.Length ?? 0) > 1000) return BadRequest(); if (!await db.Opportunities.AnyAsync(x => x.Id == id && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived), ct)) return NotFound(); db.Reports.Add(new() { OpportunityId = id, Reason = reason, Details = details ?? "" }); await db.SaveChangesAsync(ct); TempData["Notice"] = "ReportReceived"; return RedirectToAction(nameof(Details), new { id }); }
    [HttpPost] public IActionResult Language(string culture, string? returnUrl) { if (culture is not ("ar" or "en")) return BadRequest(); Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new(culture)), new() { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps }); return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/"); }
    public async Task<IActionResult> About(CancellationToken ct) => View(await ReadSitePageAsync("about", ct));
    [HttpGet] public IActionResult Contact() => View(new ContactInput());
    [HttpPost, EnableRateLimiting("contact")]
    public async Task<IActionResult> Contact(ContactInput input, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Website)) { TempData["Notice"] = "ContactReceived"; return RedirectToAction(nameof(Contact)); }
        input.Name = input.Name?.Trim() ?? ""; input.Email = input.Email?.Trim() ?? ""; input.Message = input.Message?.Trim() ?? "";
        if (input.Email.Any(char.IsControl) || !System.Net.Mail.MailAddress.TryCreate(input.Email, out var address) || !string.Equals(address.Address, input.Email, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(input.Email), "ContactEmailInvalid");
        if (!ModelState.IsValid) return View(input);
        db.ContactMessages.Add(new ContactMessage { Name = input.Name, Email = input.Email, Message = input.Message });
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "ContactReceived";
        return RedirectToAction(nameof(Contact));
    }
    public async Task<IActionResult> Privacy(CancellationToken ct) => View(await ReadSitePageAsync("privacy", ct));
    private async Task<SitePageViewModel> ReadSitePageAsync(string key, CancellationToken ct)
    {
        var page = await db.SitePages.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "ar";
        return SitePageDefaults.Resolve(key, page, language);
    }
    public IActionResult Error() => View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
