using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class AiPromptController : AdminControllerBase
{
    public AiPromptController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        PromptAdministrationService prompts
    )
        : base(database, urls, settings) => this.prompts = prompts;

    private readonly PromptAdministrationService prompts;

    public async Task<IActionResult> AiPrompt(int? inspect, CancellationToken ct)
    {
        var settings = await prompts.SettingsAsync(ct);
        var id = inspect ?? settings.PendingPromptVersionId ?? settings.ActivePromptVersionId;
        var prompt = await prompts.ReadAsync(id, ct);
        if (prompt is null)
            return NotFound();
        var input = new PromptEditor
        {
            Template = prompt.Template,
            SummaryLimit = prompt.SummaryLimit,
            GeographicScope = prompt.GeographicScope,
            AllowedKinds = prompt.AllowedKinds,
            AllowUnknownDeadline = prompt.AllowUnknownDeadline,
            Version = Convert.ToBase64String(settings.Version),
            Note = inspect is null ? "" : $"Based on version {id}",
        };
        await FillPromptAsync(input, ct);
        return View(input);
    }

    /// <summary>يفتح مسودة تصنيف للنموذج؛ الحفظ المعتاد وحده ينشئ نسخة جديدة ويجدول تفعيلها.</summary>
    [HttpGet]
    public async Task<IActionResult> CategoryPolicy(CancellationToken ct)
    {
        var result = await AiPrompt(inspect: null, ct);
        if (result is not ViewResult { Model: PromptEditor input } view) return result;
        if (!input.Template.Contains(PromptDefaults.CategoryPolicyMarker, StringComparison.Ordinal))
            input.Template += "\n" + PromptDefaults.CategoryInstructions;
        input.Note = "Canonical category inference and main fields with specialties";
        view.ViewName = "AiPrompt";
        return view;
    }

    [HttpPost]
    public async Task<IActionResult> AiPrompt(
        [Bind(
            "Template,SummaryLimit,GeographicScope,AllowedKinds,AllowUnknownDeadline,Note,Version"
        )]
            PromptEditor input,
        CancellationToken ct
    )
    {
        var kinds = PromptInputRules.NormalizeKinds(input.AllowedKinds);
        if (!PromptInputRules.ValidKinds(kinds))
            ModelState.AddModelError(
                nameof(input.AllowedKinds),
                "اختر أنواعًا صحيحة: Job, Training, Tender, ProcurementNotice."
            );
        if (PromptInputRules.HasUnknownVariables(input.Template))
            ModelState.AddModelError(nameof(input.Template), "يوجد متغير غير معروف داخل النص.");
        byte[] version = [];
        try
        {
            version = Convert.FromBase64String(input.Version ?? "");
        }
        catch (FormatException)
        {
            ModelState.AddModelError("", "نسخة الإعدادات غير صالحة؛ أعد تحميل الصفحة.");
        }
        if (!ModelState.IsValid)
        {
            await FillPromptAsync(input, ct);
            return View(input);
        }
        var outcome = await prompts.SaveAsync(new(
            input.Template ?? "", input.SummaryLimit, input.GeographicScope, string.Join(',', kinds),
            input.AllowUnknownDeadline, input.Note ?? "", version), User.Identity?.Name ?? "Admin", ct);
        if (outcome == RecordUpdateOutcome.Conflict)
        {
            ModelState.AddModelError("", "تغيّرت الإعدادات من نافذة أخرى؛ أعد التحميل قبل حفظ نسخة جديدة.");
            await FillPromptAsync(input, ct);
            return View(input);
        }
        TempData["Notice"] = "PromptQueued";
        return RedirectToAction(nameof(AiPrompt));
    }

    private async Task FillPromptAsync(PromptEditor input, CancellationToken ct)
    {
        input.Settings = await prompts.SettingsAsync(ct);
        int.TryParse(Request.Query["historyPage"], out var historyPage);
        var history = await prompts.History().PageAsync(historyPage, ct, "historyPage", "#prompt-history");
        input.History = history.Items;
        input.HistoryPager = history.Pager;
    }

    [HttpPost]
    public async Task<IActionResult> PreviewPrompt(PromptEditor input, CancellationToken ct)
    {
        await FillPromptAsync(input, ct);
        return View("AiPrompt", input);
    }
}
