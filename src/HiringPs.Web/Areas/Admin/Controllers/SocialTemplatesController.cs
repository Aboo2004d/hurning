using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[
    Area("Admin"),
    Authorize(Roles = "Admin"),
    ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)
]
public sealed class SocialTemplatesController(HiringDb db, SocialPublishing publishing, SocialTemplateManagementService? managementService = null) : Controller
{
    private SocialTemplateManagementService Templates => managementService ?? new(db);

    private static bool ValidScope(string provider, string scope) =>
        SocialTemplateManagementService.ValidScope(provider, scope);

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await Templates.Versions()
                .Where(x => x.Active)
                .OrderBy(x => x.Provider)
                .ThenBy(x => x.Scope)
                .PageAsync(page, ct)
        );

    public async Task<IActionResult> Edit(
        string provider,
        string scope = "general",
        int page = 1,
        CancellationToken ct = default
    )
    {
        if (!ValidScope(provider, scope))
            return BadRequest();
        var active = await Templates.Versions()
            .SingleOrDefaultAsync(x => x.Provider == provider && x.Scope == scope && x.Active, ct);
        ViewBag.History = await Templates.Versions()
            .Where(x => x.Provider == provider && x.Scope == scope)
            .OrderByDescending(x => x.Number)
            .PageAsync(page, ct);
        return View(
            new SocialTemplateInput
            {
                Provider = provider,
                Scope = scope,
                Body = active?.Body ?? SocialTemplateText.DefaultBody,
                ExpectedActiveId = active?.Id ?? 0,
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Preview(string body, long opportunityId, CancellationToken ct)
    {
        if (!SocialTemplateText.Valid(body ?? ""))
            return BadRequest();
        var item = await db
            .Opportunities.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opportunityId, ct);
        if (item is null)
            return NotFound();
        return Json(
            new
            {
                text = SocialTemplateText.Render(body!, item)
                    + "\n\n"
                    + publishing.OpportunityUrl(item.Id, item.OriginalLanguage),
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        SocialTemplateInput input,
        long? restoreId,
        CancellationToken ct
    )
    {
        if (!ValidScope(input.Provider, input.Scope))
            return BadRequest();
        if (restoreId is { } id)
        {
            var version = await Templates.Versions()
                .SingleOrDefaultAsync(
                    x => x.Id == id && x.Provider == input.Provider && x.Scope == input.Scope,
                    ct
                );
            if (version is null)
                return NotFound();
            input.Body = version.Body;
            ModelState.Clear();
        }
        if (!ModelState.IsValid || !SocialTemplateText.Valid(input.Body))
        {
            ModelState.AddModelError(nameof(input.Body), "SocialTemplateInvalid");
            ViewData["TemplateInvalid"] = true;
            ViewBag.History = await Templates.Versions()
                .Where(x => x.Provider == input.Provider && x.Scope == input.Scope)
                .OrderByDescending(x => x.Number)
                .PageAsync(1, ct);
            return View("Edit", input);
        }
        var outcome = await Templates.SaveAsync(new SocialTemplateSaveCommandDto(input.Provider,
            input.Scope, input.ExpectedActiveId, input.Body, User.Identity?.Name ?? "Admin"), ct);
        if (outcome == SocialTemplateSaveOutcome.InvalidRequest) return BadRequest();
        if (outcome == SocialTemplateSaveOutcome.Conflict)
        {
            TempData["Notice"] = "SocialTemplateConflict";
            return RedirectToAction(nameof(Edit), new { provider = input.Provider, scope = input.Scope });
        }
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(
            nameof(Edit),
            new { provider = input.Provider, scope = input.Scope }
        );
    }
}
