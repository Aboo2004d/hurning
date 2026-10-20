using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class DiscountsController(HiringDb db, MembershipService membership, DiscountManagementService? managementService = null) : Controller
{
    private DiscountManagementService Discounts => managementService ?? new(db, membership);

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(await Discounts.Discounts().PageAsync(page, ct));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await SetPlansAsync(ct);
        return View(
            "DiscountForm",
            new DiscountInput { ExpiresAt = DateTimeOffset.UtcNow.AddMonths(1), MaximumUses = 100 }
        );
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var code = await Discounts.FindAsync(id.Value, ct);
        if (code is null)
            return NotFound();
        await SetPlansAsync(ct);
        return View("DiscountForm", new DiscountInput
        {
            Id = code.Id, Code = code.Code, PlanId = code.PlanId, Percent = code.Percent,
            ExpiresAt = code.ExpiresAt, MaximumUses = code.MaximumUses, Enabled = code.Enabled,
        });
    }

    private async Task SetPlansAsync(CancellationToken ct) =>
        ViewBag.Plans = await Discounts.PlansAsync(ct);

    [HttpPost]
    public Task<IActionResult> Create(DiscountInput input, CancellationToken ct)
    {
        if (input.Id != 0)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    [HttpPost]
    public Task<IActionResult> Edit([FromRoute] int id, DiscountInput input, CancellationToken ct)
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    private async Task<IActionResult> SaveAsync(DiscountInput input, CancellationToken ct)
    {
        if (
            !ModelState.IsValid
            || input.Percent is < 0 or > 100
            || input.MaximumUses < 1
            || string.IsNullOrWhiteSpace(input.Code)
            || input.Code.Length > 50
        )
            return BadRequest();
        var result = await Discounts.SaveAsync(new DiscountSaveCommandDto(input.Id, input.Code,
            input.PlanId, input.Percent, input.ExpiresAt, input.MaximumUses, input.Enabled,
            User.Identity?.Name ?? "Admin"), ct);
        if (result == DiscountSaveOutcome.NotFound) return NotFound();
        if (result == DiscountSaveOutcome.InvalidRequest) return BadRequest();
        if (result == DiscountSaveOutcome.UsedImmutable) TempData["Notice"] = "DiscountUsedImmutable";
        return RedirectToAction(nameof(Index));
    }
}
