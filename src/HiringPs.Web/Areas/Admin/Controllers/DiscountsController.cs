using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class DiscountsController(HiringDb db, MembershipService membership) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await db.DiscountCodes.AsNoTracking().OrderByDescending(x => x.Id).PageAsync(page, ct)
        );

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await SetPlansAsync(ct);
        return View(
            "DiscountForm",
            new DiscountCode { ExpiresAt = DateTimeOffset.UtcNow.AddMonths(1), MaximumUses = 100 }
        );
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return RedirectToAction(nameof(Create));
        var code = await db.DiscountCodes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (code is null)
            return NotFound();
        await SetPlansAsync(ct);
        return View("DiscountForm", code);
    }

    private async Task SetPlansAsync(CancellationToken ct) =>
        ViewBag.Plans = await db
            .MembershipPlanVersions.AsNoTracking()
            .Where(x => db.MembershipPlans.Any(p => p.CurrentVersionId == x.Id))
            .ToListAsync(ct);

    [HttpPost]
    public Task<IActionResult> Create(DiscountCode input, CancellationToken ct)
    {
        if (input.Id != 0)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    [HttpPost]
    public Task<IActionResult> Edit([FromRoute] int id, DiscountCode input, CancellationToken ct)
    {
        if (id <= 0 || input.Id != id)
            return Task.FromResult<IActionResult>(BadRequest());
        return SaveAsync(input, ct);
    }

    private async Task<IActionResult> SaveAsync(DiscountCode input, CancellationToken ct)
    {
        if (
            !ModelState.IsValid
            || input.Percent is < 0 or > 100
            || input.MaximumUses < 1
            || string.IsNullOrWhiteSpace(input.Code)
            || input.Code.Length > 50
        )
            return BadRequest();
        if (
            input.PlanId is <= 0
            || input.PlanId is int planId
                && !await db.MembershipPlans.AnyAsync(x => x.Id == planId, ct)
        )
            return BadRequest();
        input.Code = input.Code.Trim().ToUpperInvariant();
        var code =
            input.Id == 0
                ? new DiscountCode()
                : await db.DiscountCodes.SingleOrDefaultAsync(x => x.Id == input.Id, ct);
        if (code is null)
            return NotFound();
        if (input.Id == 0)
            db.DiscountCodes.Add(code);
        else if (
            await db.PaymentOrders.AnyAsync(x => x.DiscountCodeId == code.Id, ct)
            && (
                code.Code != input.Code
                || code.Percent != input.Percent
                || code.PlanId != input.PlanId
            )
        )
        {
            TempData["Notice"] = "DiscountUsedImmutable";
            return RedirectToAction(nameof(Index));
        }
        if (await db.DiscountCodes.AnyAsync(x => x.Code == input.Code && x.Id != input.Id, ct))
            return BadRequest();
        code.Code = input.Code;
        code.Percent = input.Percent;
        code.PlanId = input.PlanId;
        code.ExpiresAt = input.ExpiresAt;
        code.MaximumUses = input.MaximumUses;
        code.Enabled = input.Enabled;
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Discount.Save",
            $"Code {input.Code}; enabled={code.Enabled}"
        );
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
