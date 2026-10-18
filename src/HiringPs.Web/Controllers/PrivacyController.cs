using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class PrivacyController(HiringDb db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
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
}
