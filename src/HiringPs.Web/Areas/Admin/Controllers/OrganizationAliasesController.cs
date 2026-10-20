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

    private OrganizationAliasService Aliases => new(db);

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(await Aliases.Aliases().PageAsync(page, ct));

    [HttpPost]
    public async Task<IActionResult> Create(string alias, string canonical, CancellationToken ct)
    {
        var result = await Aliases.CreateAsync(alias, canonical, User.Identity?.Name ?? "Admin", ct);
        TempData["PipelineNotice"] = result switch
        {
            OrganizationAliasSaveOutcome.Empty => Arabic ? "أدخل الاسم البديل والاسم المعتمد." : "Enter both the alias and the canonical name.",
            OrganizationAliasSaveOutcome.SameName => Arabic ? "الاسمان متطابقان بعد التوحيد." : "Both names are identical after normalisation.",
            OrganizationAliasSaveOutcome.AlreadyExists => Arabic ? "هذا الاسم البديل مسجل مسبقًا." : "This alias already exists.",
            OrganizationAliasSaveOutcome.ChainNotAllowed => Arabic ? "هذا الاسم معتمد لأسماء أخرى؛ لا تُنشأ سلاسل أسماء." : "This name is canonical for other aliases; chains are not allowed.",
            OrganizationAliasSaveOutcome.SaveFailed => Arabic ? "تعذر الحفظ؛ ربما أُضيف الاسم للتو." : "Save failed; the alias may have just been added.",
            _ => Arabic ? "تمت إضافة الاسم البديل." : "Alias added.",
        };
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await Aliases.DeleteAsync(id, User.Identity?.Name ?? "Admin", ct)) return NotFound();
        return RedirectToAction(nameof(Index));
    }
}
