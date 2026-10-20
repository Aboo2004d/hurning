using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HiringPs.Web.Controllers;

public class OpportunitiesController(
    OpportunityDetailsReadService opportunities,
    OpportunityReportService reports,
    SavedOpportunityService saved,
    MembershipService membership,
    PublicCatalog catalog
) : Controller
{
    public async Task<IActionResult> Details(long id, string? slug, CancellationToken ct)
    {
        var item = await opportunities.ReadAsync(id, ct);
        if (item is null)
            return NotFound();
        // The canonical URL carries the title slug; a wrong or missing slug is permanently redirected.
        var expected = UrlSlug.Create(item.Title);
        if (
            (HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method))
            && !string.Equals(slug ?? "", expected, StringComparison.Ordinal)
        )
            return RedirectPermanent(
                Url.Content("~" + PublicListing.DetailsPath(item)) + Request.QueryString
            );
        var allowed = await membership.DetailKindsAsync(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.IsInRole("Admin"),
            ct
        );
        if (!allowed.Contains(item.Kind))
        {
            Response.StatusCode = 403;
            return View("Restricted", item.Kind);
        }
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            var memberId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            ViewBag.IsSaved = await saved.ContainsAsync(memberId!, id, ct);
            Response.Headers.CacheControl = "no-store";
        }
        ViewData["ListingKind"] = item.Kind;
        var facets = await catalog.FacetsAsync(ct);
        ViewBag.OrganizationFacet = facets.OrganizationFor(item.NormalizedOrganization);
        ViewBag.CityFacets = facets
            .Cities.Where(x =>
                x.Kind == item.Kind
                && item.LocationSearchText.Contains(x.City.SearchKey, StringComparison.Ordinal)
            )
            .Select(x => x.City)
            .ToList();
        ViewBag.VerifiedDates = await opportunities.DatesAsync(item.Id, ct);
        return View(item);
    }

    [HttpPost, EnableRateLimiting("public")]
    public async Task<IActionResult> Report(
        long id,
        string reason,
        string? details,
        CancellationToken ct
    )
    {
        if (!OpportunityReportService.Valid(reason, details))
            return BadRequest();
        var title = await reports.SubmitAsync(id, reason, details, ct);
        if (title is null)
            return NotFound();
        TempData["Notice"] = "ReportReceived";
        return LocalRedirect(Url.Content("~" + PublicListing.DetailsPath(id, title)));
    }
}
