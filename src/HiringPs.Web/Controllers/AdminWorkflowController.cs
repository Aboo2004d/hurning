using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public partial class AdminController
{
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

    public async Task<IActionResult> AiUsage(
        int days = 30,
        int page = 1,
        int groupsPage = 1,
        CancellationToken ct = default
    )
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var query = db.AiUsage.AsNoTracking().Where(x => x.StartedAt >= since);
        var items = await query
            .OrderByDescending(x => x.Id)
            .PageAsync(page, ct, "page", "#ai-requests");
        var groupsQuery = query
            .GroupBy(x => new { x.PromptVersionId, x.Model })
            .Select(g => new AiUsageTotals
            {
                PromptVersionId = g.Key.PromptVersionId,
                Model = g.Key.Model,
                Requests = g.Count(),
                Succeeded = g.Count(x => x.Success == true),
                Failed = g.Count(x => x.Success == false),
                Unfinished = g.Count(x => x.Success == null),
                UnknownTokenRequests = g.Count(x => x.TotalTokens == null),
                InputTokens =
                    g.Count(x => x.InputTokens != null) > 0
                        ? g.Sum(x => (long?)x.InputTokens)
                        : null,
                OutputTokens =
                    g.Count(x => x.OutputTokens != null) > 0
                        ? g.Sum(x => (long?)x.OutputTokens)
                        : null,
                TotalTokens =
                    g.Count(x => x.TotalTokens != null) > 0
                        ? g.Sum(x => (long?)x.TotalTokens)
                        : null,
            });
        var groups = await groupsQuery
            .OrderByDescending(x => x.PromptVersionId)
            .ThenBy(x => x.Model)
            .PageAsync(groupsPage, ct, "groupsPage", "#ai-totals");
        return View(
            new AiUsageDashboard
            {
                Days = days,
                Items = items.Items,
                ItemsPager = items.Pager,
                Groups = groups.Items,
                GroupsPager = groups.Pager,
            }
        );
    }

    public async Task<IActionResult> Retention(CancellationToken ct)
    {
        var policy = await db.RetentionPolicies.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
        return View(
            new RetentionEditor
            {
                Enabled = policy.Enabled,
                RevisionDays = policy.RevisionDays,
                DiagnosticDays = policy.DiagnosticDays,
                Version = Convert.ToBase64String(policy.Version),
                Current = policy,
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Retention(
        [Bind("Enabled,RevisionDays,DiagnosticDays,Version")] RetentionEditor input,
        CancellationToken ct
    )
    {
        var policy = await db.RetentionPolicies.SingleAsync(x => x.Id == 1, ct);
        input.Current = policy;
        byte[] version = [];
        try
        {
            version = Convert.FromBase64String(input.Version ?? "");
        }
        catch (FormatException)
        {
            ModelState.AddModelError("", "أعد تحميل الإعدادات.");
        }
        if (!ModelState.IsValid)
            return View(input);
        db.Entry(policy).Property(x => x.Version).OriginalValue = version;
        policy.Enabled = input.Enabled;
        policy.RevisionDays = input.RevisionDays;
        policy.DiagnosticDays = input.DiagnosticDays;
        policy.LastCleanupAt = null;
        Audit(
            "Retention.Updated",
            $"Enabled={policy.Enabled}; revisions={policy.RevisionDays}; diagnostics={policy.DiagnosticDays}"
        );
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "تغيّرت الإعدادات؛ أعد تحميل الصفحة.");
            return View(input);
        }
        TempData["Notice"] = "RetentionSaved";
        return RedirectToAction(nameof(Retention));
    }

    public async Task<IActionResult> Merges(
        long? opportunityId,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var query = db.MergeOperations.AsNoTracking();
        if (opportunityId is { } id)
            query = query.Where(x => x.DuplicateId == id || x.TargetId == id);
        return View(await query.OrderByDescending(x => x.Id).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> UndoMerge(long id, CancellationToken ct)
    {
        try
        {
            TempData["Notice"] = await MergeHistory.UndoAsync(
                db,
                id,
                User.Identity?.Name ?? "Admin",
                ct
            )
                ? "MergeUndone"
                : "MergeUndoConflict";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Notice"] = "MergeUndoConflict";
        }
        return RedirectToAction(nameof(Merges));
    }
}
