using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class PagesController : AdminControllerBase
{
    public PagesController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        SiteContentService service
    )
        : base(database, urls, settings) => ContentService = service;

    private SiteContentService ContentService
    {
        get;
    }

    public async Task<IActionResult> Pages(CancellationToken ct) => View(await ContentService.PagesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> PageContent(string key, CancellationToken ct)
    {
        if (!SitePageDefaults.IsEditable(key))
            return NotFound();
        var page = await ContentService.ReadAsync(key, ct);
        var ar = SitePageDefaults.Resolve(key, page, "ar");
        var en = SitePageDefaults.Resolve(key, page, "en");
        return View(new SitePageInput
        {
            Key = key,
            TitleAr = ar.Title,
            BodyAr = ar.Body,
            TitleEn = en.Title,
            BodyEn = en.Body,
            Version = page?.Version ?? []
        });
    }

    [HttpPost]
    public async Task<IActionResult> PageContent(SitePageInput input, CancellationToken ct)
    {
        NormalizeStrings(input);
        if (!SitePageDefaults.IsEditable(input.Key))
            return NotFound();
        if (!ModelState.IsValid)
            return View(input);
        var result = await ContentService.SavePageAsync(new(input.Key, input.TitleAr, input.BodyAr,
            input.TitleEn, input.BodyEn, input.Version), User.Identity?.Name ?? "Admin", ct);
        if (result == RecordUpdateOutcome.NotFound)
            return NotFound();
        if (result == RecordUpdateOutcome.Conflict)
        {
            ModelState.AddModelError("", "PageEditConflict");
            return View(input);
        }
        TempData["Notice"] = "PageContentSaved";
        return RedirectToAction(nameof(PageContent), new
        {
            key = input.Key
        });
    }
}
