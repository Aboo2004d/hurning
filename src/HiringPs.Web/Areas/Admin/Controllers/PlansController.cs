using HiringPs.Core;
using HiringPs.Web.Models;
using HiringPs.Web.Services.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class PlansController(HiringDb db, MembershipService membership, AdminPlanReadService? readService = null,
    PlanManagementService? managementService = null, PlanCatalogNotifications? notifications = null, OpportunityAccessService? accessService = null) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var reader = readService ?? new AdminPlanReadService(db);
        var result = await reader.Plans().PageAsync(page, ct);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Plan-Catalog"] == "true" ? PartialView("_PlanTable", result) : View(result);
    }

    [HttpGet]
    public IActionResult Create() => View("PlanForm", new PlanInput());

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var reader = readService ?? new AdminPlanReadService(db);
        var plan = await reader.EditAsync(id.Value, ct);
        if (plan is null) return NotFound();
        var version = plan.CurrentVersion;
        return View(
            "PlanForm",
            new PlanInput
            {
                Id = id,
                NameAr = version.NameAr,
                NameEn = version.NameEn,
                DescriptionAr = version.DescriptionAr,
                DescriptionEn = version.DescriptionEn,
                BadgeAr = version.BadgeAr,
                BadgeEn = version.BadgeEn,
                PriceIls = version.PriceIls,
                Days = version.Days,
                MaxPreferredCategories = version.MaxPreferredCategories,
                Details = Enum.GetValues<OpportunityKind>()
                    .Where(x => MembershipRules.Includes(version.DetailKinds, x))
                    .ToArray(),
                Alerts = Enum.GetValues<OpportunityKind>()
                    .Where(x => MembershipRules.Includes(version.AlertKinds, x))
                    .ToArray(),
                Visible = plan.Visible,
                SortOrder = plan.SortOrder,
                Version = plan.Version,
            }
        );
    }

    [HttpPost]
    public Task<IActionResult> Create(PlanInput input, CancellationToken ct)
    {
        if (input.Id is not null)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    [HttpPost]
    public Task<IActionResult> Edit([FromRoute] int id, PlanInput input, CancellationToken ct)
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    private async Task<IActionResult> SaveAsync(PlanInput input, CancellationToken ct)
    {
        if (input.Details.Concat(input.Alerts).Any(x => !Enum.IsDefined(x)))
            return BadRequest();
        if (!ModelState.IsValid)
            return View("PlanForm", input);
        var manager = managementService ?? new PlanManagementService(db, membership);
        var result = await manager.SaveAsync(new PlanSaveCommandDto(input.Id, input.NameAr, input.NameEn,
            input.DescriptionAr ?? "", input.DescriptionEn ?? "", input.BadgeAr ?? "", input.BadgeEn ?? "",
            input.PriceIls, input.Days, input.Details, input.Alerts, input.Visible, input.SortOrder,
            input.Version, User.Identity?.Name ?? "Admin", input.MaxPreferredCategories), ct);
        if (result.Outcome == PlanSaveOutcome.NotFound) return NotFound();
        if (result.Outcome == PlanSaveOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == PlanSaveOutcome.Conflict)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError("", "PlanEditConflict");
            return View("PlanForm", input);
        }
        notifications?.Changed();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Access(CancellationToken ct) =>
        View(await (accessService ?? new OpportunityAccessService(db, membership)).ReadAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Access(OpportunityKind[] publicKinds, CancellationToken ct)
    {
        if (!await (accessService ?? new OpportunityAccessService(db, membership))
            .SaveAsync(publicKinds, User.Identity?.Name ?? "Admin", ct)) return BadRequest();
        return RedirectToAction(nameof(Access));
    }
}
