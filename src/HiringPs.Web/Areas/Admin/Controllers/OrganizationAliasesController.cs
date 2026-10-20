using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

// Alternative organisation spellings that duplicate matching treats as one organisation.
public class OrganizationAliasesController : AdminControllerBase
{
    public OrganizationAliasesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    private static bool Arabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await db
                .OrganizationAliases.AsNoTracking()
                .OrderBy(x => x.Canonical)
                .ThenBy(x => x.Alias)
                .PageAsync(page, ct)
        );

    [HttpPost]
    public async Task<IActionResult> Create(string alias, string canonical, CancellationToken ct)
    {
        var aliasKey = TextRules.OrganizationKey(alias ?? "");
        var canonicalKey = TextRules.OrganizationKey(canonical ?? "");
        string? error = null;
        if (aliasKey.Length == 0 || canonicalKey.Length == 0)
            error = Arabic ? "أدخل الاسم البديل والاسم المعتمد." : "Enter both the alias and the canonical name.";
        else if (aliasKey == canonicalKey)
            error = Arabic ? "الاسمان متطابقان بعد التوحيد." : "Both names are identical after normalisation.";
        else if (await db.OrganizationAliases.AnyAsync(x => x.Alias == aliasKey, ct))
            error = Arabic ? "هذا الاسم البديل مسجل مسبقًا." : "This alias already exists.";
        else if (await db.OrganizationAliases.AnyAsync(x => x.Canonical == aliasKey, ct))
            error = Arabic
                ? "هذا الاسم معتمد لأسماء أخرى؛ لا تُنشأ سلاسل أسماء."
                : "This name is canonical for other aliases; chains are not allowed.";
        if (error is null)
        {
            // An alias of an alias resolves directly to the final canonical name.
            var final =
                await db
                    .OrganizationAliases.Where(x => x.Alias == canonicalKey)
                    .Select(x => x.Canonical)
                    .FirstOrDefaultAsync(ct)
                ?? canonicalKey;
            db.OrganizationAliases.Add(
                new()
                {
                    Alias = aliasKey,
                    Canonical = final,
                    CreatedBy = (User.Identity?.Name ?? "Admin").Truncate(256),
                }
            );
            Audit("OrganizationAlias.Create", $"{aliasKey} -> {final}".Truncate(2000));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                error = Arabic ? "تعذر الحفظ؛ ربما أُضيف الاسم للتو." : "Save failed; the alias may have just been added.";
            }
        }
        TempData["PipelineNotice"] = error ?? (Arabic ? "تمت إضافة الاسم البديل." : "Alias added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var alias = await db.OrganizationAliases.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (alias is null)
            return NotFound();
        db.OrganizationAliases.Remove(alias);
        Audit("OrganizationAlias.Delete", $"{alias.Alias} -> {alias.Canonical}".Truncate(2000));
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
