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

    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        ViewBag.Plans = await db
            .MembershipPlanVersions.AsNoTracking()
            .Where(x => db.MembershipPlans.Any(p => p.CurrentVersionId == x.Id))
            .ToListAsync(ct);
        return View(
            id is null
                ? new DiscountCode
                {
                    ExpiresAt = DateTimeOffset.UtcNow.AddMonths(1),
                    MaximumUses = 100,
                }
                : await db.DiscountCodes.SingleAsync(x => x.Id == id, ct)
        );
    }

    [HttpPost]
    public async Task<IActionResult> Edit(DiscountCode input, CancellationToken ct)
    {
        if (
            !ModelState.IsValid
            || input.Percent is < 0 or > 100
            || input.MaximumUses < 1
            || string.IsNullOrWhiteSpace(input.Code)
            || input.Code.Length > 50
        )
            return BadRequest();
        input.Code = input.Code.Trim().ToUpperInvariant();
        var code =
            input.Id == 0
                ? new DiscountCode()
                : await db.DiscountCodes.SingleAsync(x => x.Id == input.Id, ct);
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
