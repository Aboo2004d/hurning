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

public class AiPromptController : AdminControllerBase
{
    public AiPromptController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> AiPrompt(int? inspect, CancellationToken ct)
    {
        var settings = await db.WorkflowSettings.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
        var id = inspect ?? settings.PendingPromptVersionId ?? settings.ActivePromptVersionId;
        var prompt = await db
            .PromptVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
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

    [HttpPost]
    public async Task<IActionResult> AiPrompt(
        [Bind(
            "Template,SummaryLimit,GeographicScope,AllowedKinds,AllowUnknownDeadline,Note,Version"
        )]
            PromptEditor input,
        CancellationToken ct
    )
    {
        var kinds = (input.AllowedKinds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToArray();
        if (
            kinds.Length == 0
            || kinds.Any(x =>
                !Enum.TryParse<OpportunityKind>(x, out var kind)
                || !Enum.IsDefined(kind)
                || kind.ToString() != x
            )
        )
            ModelState.AddModelError(
                nameof(input.AllowedKinds),
                "اختر أنواعًا صحيحة: Job, Training, Tender, ProcurementNotice."
            );
        var allowed = new[] { "summaryLimit", "scope", "allowedKinds", "allowUnknownDeadline" };
        if (
            Regex
                .Matches(input.Template ?? "", @"\{\{(.*?)\}\}")
                .Any(m => !allowed.Contains(m.Groups[1].Value))
        )
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
        var settings = await db.WorkflowSettings.SingleAsync(x => x.Id == 1, ct);
        db.Entry(settings).Property(x => x.Version).OriginalValue = version;
        var prompt = new PromptVersion
        {
            Template = input.Template ?? "",
            SummaryLimit = input.SummaryLimit,
            GeographicScope = input.GeographicScope.Trim(),
            AllowedKinds = string.Join(',', kinds),
            AllowUnknownDeadline = input.AllowUnknownDeadline,
            Note = input.Note ?? "",
            CreatedBy = User.Identity?.Name ?? "Admin",
        };
        prompt.FinalText = prompt.Compose();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PromptVersions.Add(prompt);
            await db.SaveChangesAsync(ct);
            settings.PendingPromptVersionId = prompt.Id;
            Audit(
                "Prompt.VersionCreated",
                $"Version {prompt.Id}; activation queued for the next worker round."
            );
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            ModelState.AddModelError(
                "",
                "تغيّرت الإعدادات من نافذة أخرى؛ أعد التحميل قبل حفظ نسخة جديدة."
            );
            await FillPromptAsync(input, ct);
            return View(input);
        }
        TempData["Notice"] = "PromptQueued";
        return RedirectToAction(nameof(AiPrompt));
    }

    private async Task FillPromptAsync(PromptEditor input, CancellationToken ct)
    {
        input.Settings = await db.WorkflowSettings.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
        int.TryParse(Request.Query["historyPage"], out var historyPage);
        var history = await db
            .PromptVersions.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .PageAsync(historyPage, ct, "historyPage", "#prompt-history");
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
