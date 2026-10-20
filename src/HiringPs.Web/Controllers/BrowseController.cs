// صفحات تصفح قابلة للفهرسة: نوع الفرصة في مدينة، وفرص جهة واحدة. تعيد 404 إذا لم توجد فرص حالية.
using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Localization;

namespace HiringPs.Web.Controllers;

public class BrowseController(
    HiringPs.Web.Services.PublicCatalog.PublicListingReadService listings,
    SavedOpportunityService saved,
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
        var result = await listings.CityAsync(kind.Value, city.SearchKey, page, TextRules.PalestineToday(), ct);
        if (result is null)
            return NotFound();
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
        return await RenderAsync(model, result, page, ct);
    }

    [OutputCache(PolicyName = SiteCache.Listing)]
    public async Task<IActionResult> Organization(string orgSlug, int page = 1, CancellationToken ct = default)
    {
        var facets = await catalog.FacetsAsync(ct);
        var organization = facets.Organization(orgSlug);
        if (organization is null)
            return NotFound();
        var result = await listings.OrganizationAsync(organization.Keys, page, TextRules.PalestineToday(), ct);
        if (result is null)
            return NotFound();
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
        return await RenderAsync(model, result, page, ct);
    }

    private async Task<IActionResult> RenderAsync(
        BrowseModel model,
        OpportunityPageDto result,
        int page,
        CancellationToken ct
    )
    {
        model.Count = result.Count;
        model.Page = page;
        model.Items = result.Items;
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
            model.SavedIds = await saved.IdsAsync(memberId!, ids, ct);
            Response.Headers.CacheControl = "no-store";
        }
        return View("Index", model);
    }

    internal static string KindLabel(OpportunityKind kind) =>
        kind == OpportunityKind.Job ? "Jobs" : kind.ToString();
}
