using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class SocialTemplateInput
{
    public string Provider { get; set; } = "Facebook";
    public string Scope { get; set; } = "general";

    [Required, MaxLength(50000)]
    public string Body { get; set; } = "";
    public long ExpectedActiveId { get; set; }
}

[
    Area("Admin"),
    Authorize(Roles = "Admin"),
    ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)
]
public sealed class SocialTemplatesController(HiringDb db, SocialPublishing publishing) : Controller
{
    private static bool ValidScope(string provider, string scope) =>
        SocialPublishing.IsProvider(provider)
        && (scope == "general" || Enum.GetNames<OpportunityKind>().Contains(scope));

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await db
                .SocialTemplateVersions.AsNoTracking()
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
        var active = await db
            .SocialTemplateVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Provider == provider && x.Scope == scope && x.Active, ct);
        ViewBag.History = await db
            .SocialTemplateVersions.AsNoTracking()
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
            var version = await db
                .SocialTemplateVersions.AsNoTracking()
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
            ViewBag.History = await db
                .SocialTemplateVersions.AsNoTracking()
                .Where(x => x.Provider == input.Provider && x.Scope == input.Scope)
                .OrderByDescending(x => x.Number)
                .PageAsync(1, ct);
            return View("Edit", input);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var resource = "SocialTemplate:" + input.Provider + ":" + input.Scope;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r=sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Template busy', 1;",
            ct
        );
        var active = await db.SocialTemplateVersions.SingleOrDefaultAsync(
            x => x.Provider == input.Provider && x.Scope == input.Scope && x.Active,
            ct
        );
        if ((active?.Id ?? 0) != input.ExpectedActiveId)
        {
            TempData["Notice"] = "SocialTemplateConflict";
            return RedirectToAction(
                nameof(Edit),
                new { provider = input.Provider, scope = input.Scope }
            );
        }
        var number =
            (
                await db
                    .SocialTemplateVersions.Where(x =>
                        x.Provider == input.Provider && x.Scope == input.Scope
                    )
                    .MaxAsync(x => (int?)x.Number, ct)
                ?? 0
            ) + 1;
        if (active is not null)
        {
            active.Active = false;
            await db.SaveChangesAsync(ct);
        }
        db.SocialTemplateVersions.Add(
            new SocialTemplateVersion
            {
                Provider = input.Provider,
                Scope = input.Scope,
                Number = number,
                Active = true,
                Body = input.Body.Trim(),
                Actor = User.Identity?.Name ?? "Admin",
            }
        );
        db.Audit.Add(
            new AuditEntry
            {
                Actor = User.Identity?.Name ?? "Admin",
                Action = "SocialTemplate.Version",
                Detail = input.Provider + ":" + input.Scope + ":" + number,
            }
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(
            nameof(Edit),
            new { provider = input.Provider, scope = input.Scope }
        );
    }
}
