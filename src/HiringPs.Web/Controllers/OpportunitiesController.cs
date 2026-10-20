using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class OpportunitiesController(
    HiringDb db,
    MembershipService membership,
    PublicCatalog catalog
) : Controller
{
    public async Task<IActionResult> Details(long id, string? slug, CancellationToken ct)
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
            ViewBag.IsSaved = await db
                .SavedOpportunities.AsNoTracking()
                .AnyAsync(x => x.UserId == memberId && x.OpportunityId == id, ct);
            Response.Headers.CacheControl = "no-store";
        }
        ViewData["ListingKind"] = item.Kind;
        ViewBag.Attachments = Attachments(item.AttachmentsJson);
        var facets = await catalog.FacetsAsync(ct);
        ViewBag.OrganizationFacet = facets.OrganizationFor(item.NormalizedOrganization);
        ViewBag.CityFacets = facets
            .Cities.Where(x =>
                x.Kind == item.Kind
                && item.LocationSearchText.Contains(x.City.SearchKey, StringComparison.Ordinal)
            )
            .Select(x => x.City)
            .ToList();
        ViewBag.VerifiedDates = await db
            .OpportunityDates.AsNoTracking()
            .Where(x => x.OpportunityId == item.Id)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return View(item);
    }

    /// <summary>HTTPS attachment links from stored JSON; malformed data yields no links instead of an error.</summary>
    private static List<string> Attachments(string? json)
    {
        try
        {
            return (System.Text.Json.JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? [])
                .Where(file =>
                    Uri.TryCreate(file, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                )
                .Distinct()
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
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
        var title = await db
            .Opportunities.Where(x =>
                x.Id == id
                && (
                    x.Status == PublicationStatus.Published
                    || x.Status == PublicationStatus.Archived
                )
            )
            .Select(x => x.Title)
            .FirstOrDefaultAsync(ct);
        if (title is null)
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
        return LocalRedirect(Url.Content("~" + PublicListing.DetailsPath(id, title)));
    }
}
