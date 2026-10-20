// صفحات تصفح قابلة للفهرسة: نوع الفرصة في مدينة، وفرص جهة واحدة. تعيد 404 إذا لم توجد فرص حالية.
using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace HiringPs.Web.Controllers;

public class BrowseController(
    HiringDb db,
    MembershipService membership,
    PublicCatalog catalog,
    IStringLocalizer<Labels> T
) : Controller
{
    [OutputCache(PolicyName = SiteCache.Listing)]
    public async Task<IActionResult> City(string kindSlug, string citySlug, int page = 1, CancellationToken ct = default)
    {
        var kind = KindSlugs.Parse(kindSlug);
        var city = CityCatalog.FromSlug(citySlug);
        if (kind is null || city is null)
            return NotFound();
        if (kindSlug != KindSlugs.For(kind.Value) || citySlug != city.Slug)
            return RedirectPermanent(Url.Content("~" + BrowsePaths.City(kind.Value, city)));
        var english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var key = city.SearchKey;
        var query = db
            .Opportunities.AsNoTracking()
            .Current(TextRules.PalestineToday())
            .Where(x => x.Kind == kind && x.LocationSearchText.Contains(key));
        var model = new BrowseModel
        {
            Kind = kind,
            BrowseCity = city,
            Path = BrowsePaths.City(kind.Value, city),
            Heading = string.Format(
                CultureInfo.CurrentCulture,
                T["BrowseCityHeading"].Value,
                T[KindLabel(kind.Value)].Value,
                city.Name(english)
            ),
        };
        var kindLabel = T[KindLabel(kind.Value)].Value;
        model.Intro = string.Format(
            CultureInfo.CurrentCulture,
            T["BrowseCityIntro"].Value,
            english ? kindLabel.ToLowerInvariant() : kindLabel,
            city.Name(english)
        );
        ViewData["ListingKind"] = kind;
        return await RenderAsync(model, query, page, ct);
    }

    [OutputCache(PolicyName = SiteCache.Listing)]
    public async Task<IActionResult> Organization(string orgSlug, int page = 1, CancellationToken ct = default)
    {
        var facets = await catalog.FacetsAsync(ct);
        var organization = facets.Organization(orgSlug);
        if (organization is null)
            return NotFound();
        var keys = organization.Keys;
        var query = db
            .Opportunities.AsNoTracking()
            .Current(TextRules.PalestineToday())
            .Where(x => keys.Contains(x.NormalizedOrganization));
        var model = new BrowseModel
        {
            Organization = organization,
            Path = BrowsePaths.Organization(organization.Slug),
            Heading = string.Format(
                CultureInfo.CurrentCulture,
                T["BrowseOrganizationHeading"].Value,
                organization.Name
            ),
            Intro = string.Format(
                CultureInfo.CurrentCulture,
                T["BrowseOrganizationIntro"].Value,
                organization.Name
            ),
        };
        return await RenderAsync(model, query, page, ct);
    }

    private async Task<IActionResult> RenderAsync(
        BrowseModel model,
        IQueryable<Opportunity> query,
        int page,
        CancellationToken ct
    )
    {
        var count = await query.CountAsync(ct);
        if (count == 0 || page < 1 || page > PublicListing.PageCount(count))
            return NotFound();
        model.Count = count;
        model.Page = page;
        model.Items = await query
            .OrderByDescending(x => x.SortDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * PublicListing.PageSize)
            .Take(PublicListing.PageSize)
            .ToListAsync(ct);
        model.AllowedDetails = await membership.DetailKindsAsync(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.IsInRole("Admin"),
            ct
        );
        model.PublicDetailKinds = await catalog.PublicDetailKindsAsync(ct);
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            var memberId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
        return View("Index", model);
    }

    internal static string KindLabel(OpportunityKind kind) =>
        kind == OpportunityKind.Job ? "Jobs" : kind.ToString();
}
