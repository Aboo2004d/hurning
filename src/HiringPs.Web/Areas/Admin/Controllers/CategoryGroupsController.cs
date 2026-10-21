using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CategoryGroupsController(CategoryGroupReadService reader,
    CategoryGroupSuggestionService suggestions, CategoryGroupManagementService management) : Controller
{
    /// <summary>يعرض الاقتراحات فقط؛ زيارة الصفحة لا تعتمد أو تغير أي مجموعة.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int groupPage = 1, int categoryPage = 1, CancellationToken ct = default)
    {
        if (search?.Length > 200) return BadRequest();
        var groups = await reader.Groups(search).PageAsync(groupPage, ct, "groupPage", "#approved-groups");
        var pending = await suggestions.PageAsync(search, categoryPage, AdminPager.PageSize, ct);
        return View(new CategoryGroupsPage(search, groups, pending.Items,
            new AdminPager { Page = pending.Page, TotalRecords = pending.Total, Parameter = "categoryPage", Anchor = "#ungrouped-categories" }));
    }

    /// <summary>يحضر اقتراحًا أو مجموعة فارغة لمراجعة المسؤول قبل اعتمادها.</summary>
    [HttpGet]
    public async Task<IActionResult> Create(string? first, string? second, CancellationToken ct)
    {
        var input = new CategoryGroupInput();
        await FillAsync(input, ct);
        var selected = new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => CategoryGroupMap.Key(x)).ToHashSet();
        input.Members = input.Options.Where(x => x.GroupId is null && (selected.Contains(CategoryGroupMap.Key(x.Label))
            || !string.IsNullOrWhiteSpace(second) && !string.IsNullOrWhiteSpace(first) && CategorySimilarityRules.Suggests(first, x.Label)))
            .Select(x => x.Label).ToArray();
        input.Name = input.Members.FirstOrDefault()?.Truncate(160) ?? "";
        return View("Editor", input);
    }

    /// <summary>يعرض المعتمد ويتيح إضافة مجال جديد مستقل دون إدخاله تلقائيًا في المجموعة.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? add, CancellationToken ct)
    {
        var group = await reader.FindAsync(id, ct);
        if (group is null) return NotFound();
        var input = new CategoryGroupInput { Id = id, Name = group.Name, Members = group.Members, Version = Convert.ToBase64String(group.Version) };
        await FillAsync(input, ct);
        var extra = input.Options.FirstOrDefault(x => x.GroupId is null && CategoryGroupMap.Key(x.Label) == CategoryGroupMap.Key(add));
        if (extra is not null) input.Members = input.Members.Append(extra.Label).ToArray();
        return View("Editor", input);
    }

    /// <summary>اعتماد صريح محمي ضد CSRF والتزوير والتعديل المتزامن.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategoryGroupInput input, CancellationToken ct)
    {
        byte[] version = [];
        try { version = Convert.FromBase64String(input.Version ?? ""); }
        catch (FormatException) { ModelState.AddModelError("Version", "CategoryGroupConflict"); }
        if (!ModelState.IsValid)
        {
            await FillAsync(input, ct);
            return View("Editor", input);
        }
        var outcome = await management.SaveAsync(new(input.Id, input.Name, input.Members, version, User.Identity?.Name ?? "Admin"), ct);
        if (outcome == CategoryGroupSaveOutcome.NotFound) return NotFound();
        if (outcome != CategoryGroupSaveOutcome.Saved)
        {
            ModelState.AddModelError("", outcome == CategoryGroupSaveOutcome.Conflict ? "CategoryGroupConflict" : "CategoryGroupInvalid");
            await FillAsync(input, ct);
            return View("Editor", input);
        }
        TempData["Notice"] = "CategoryGroupSaved";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>يفك التجميع مع إبقاء الإعلانات والتفضيلات الأصلية.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string version, CancellationToken ct)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { return BadRequest(); }
        var outcome = await management.DeleteAsync(id, bytes, User.Identity?.Name ?? "Admin", ct);
        if (outcome == CategoryGroupSaveOutcome.NotFound) return NotFound();
        TempData["Notice"] = outcome == CategoryGroupSaveOutcome.Saved ? "CategoryGroupDeleted" : "CategoryGroupConflict";
        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(CategoryGroupInput input, CancellationToken ct)
    {
        var catalog = await reader.CatalogAsync(ct);
        var assignments = await reader.AssignmentsAsync(ct);
        var map = await reader.MapAsync(ct);
        input.Options = catalog.Select(label => new CategoryGroupOptionDto(label,
            assignments.TryGetValue(CategoryGroupMap.Key(label), out var group) ? group : null, map.GroupName(label))).ToArray();
    }
}
