using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class PagesController : AdminControllerBase
{
    public PagesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Pages(CancellationToken ct) =>
        View(await db.SitePages.AsNoTracking().ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> PageContent(string key, CancellationToken ct)
    {
        if (!HiringPs.Web.Models.SitePageDefaults.IsEditable(key))
            return NotFound();
        var page = await db.SitePages.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        var ar = HiringPs.Web.Models.SitePageDefaults.Resolve(key, page, "ar");
        var en = HiringPs.Web.Models.SitePageDefaults.Resolve(key, page, "en");
        return View(
            new HiringPs.Web.Models.SitePageInput
            {
                Key = key,
                TitleAr = ar.Title,
                BodyAr = ar.Body,
                TitleEn = en.Title,
                BodyEn = en.Body,
                Version = page?.Version ?? [],
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> PageContent(
        HiringPs.Web.Models.SitePageInput input,
        CancellationToken ct
    )
    {
        NormalizeStrings(input);
        if (!HiringPs.Web.Models.SitePageDefaults.IsEditable(input.Key))
            return NotFound();
        if (!ModelState.IsValid)
            return View(input);
        var page = await db.SitePages.SingleOrDefaultAsync(x => x.Key == input.Key, ct);
        if (page is not null && (input.Version is null || input.Version.Length == 0))
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }

        if (page is null)
        {
            if (input.Version is { Length: > 0 })
            {
                ModelState.AddModelError("", "PageEditConflict");
                return View(input);
            }

            page = new SitePage { Key = input.Key };
            db.SitePages.Add(page);
        }
        else
            db.Entry(page).Property(x => x.Version).OriginalValue = input.Version!;
        page.TitleAr = input.TitleAr.Trim();
        page.TitleEn = input.TitleEn.Trim();
        page.BodyAr = input.BodyAr.Trim();
        page.BodyEn = input.BodyEn.Trim();
        page.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("SitePage.Update", $"Page {page.Key}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
                && sql.Number is 2601 or 2627
            )
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }

        TempData["Notice"] = "PageContentSaved";
        return RedirectToAction(nameof(PageContent), new { key = input.Key });
    }
}
