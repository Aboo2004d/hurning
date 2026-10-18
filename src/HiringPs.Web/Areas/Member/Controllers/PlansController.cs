using System.Data;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PlansController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    MembershipService membership
) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await Db
            .MembershipPlans.AsNoTracking()
            .Where(x => x.Visible && x.CurrentVersionId != null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var versions = plans.Select(x => x.CurrentVersionId!.Value).ToList();
        return View(
            new PlanCatalog
            {
                Plans = plans,
                Versions = await Db
                    .MembershipPlanVersions.AsNoTracking()
                    .Where(x => versions.Contains(x.Id))
                    .ToListAsync(ct),
                Subscriptions = await Db
                    .MemberSubscriptions.AsNoTracking()
                    .Include(x => x.PlanVersion)
                    .Where(x =>
                        x.UserId == MemberId && !x.Cancelled && x.EndsAt > DateTimeOffset.UtcNow
                    )
                    .OrderBy(x => x.StartsAt)
                    .ToListAsync(ct),
            }
        );
    }

    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        var input = new PurchaseInput { PlanId = id };
        await FillAsync(input, ct);
        input.PresentedVersionId = input.Plan?.Id ?? 0;
        input.PresentedAmount = input.FinalAmount;
        return input.Plan is null ? NotFound() : View(input);
    }

    private async Task FillAsync(PurchaseInput input, CancellationToken ct)
    {
        var plan = await Db
            .MembershipPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.PlanId && x.Visible, ct);
        input.Plan = plan is null
            ? null
            : await Db
                .MembershipPlanVersions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == plan.CurrentVersionId, ct);
        input.Gateways = (await registry.AvailableAsync(ct)).Select(x => x.Key).ToArray();
        input.FinalAmount = input.Plan?.PriceIls ?? 0;
        input.StartsAt = MembershipRules.Starts(
            DateTimeOffset.UtcNow,
            await Db
                .MemberSubscriptions.AsNoTracking()
                .Where(x => x.UserId == MemberId && !x.Cancelled)
                .ToListAsync(ct)
        );
        if (input.Plan is not null && !string.IsNullOrWhiteSpace(input.DiscountCode))
        {
            var code = input.DiscountCode.Trim().ToUpperInvariant();
            var discount = await Db
                .DiscountCodes.AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Code == code
                        && x.PlanId == input.Plan.PlanId
                        && x.Enabled
                        && x.ExpiresAt > DateTimeOffset.UtcNow,
                    ct
                );
            if (discount is not null)
                input.FinalAmount = MembershipRules.Amount(input.Plan.PriceIls, discount.Percent);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Review(
        PurchaseInput input,
        bool checkout = false,
        CancellationToken ct = default
    )
    {
        await FillAsync(input, ct);
        if (input.Plan is null)
            return NotFound();
        if (!checkout)
        {
            input.PresentedVersionId = input.Plan.Id;
            input.PresentedAmount = input.FinalAmount;
            ModelState.Remove(nameof(input.PresentedVersionId));
            ModelState.Remove(nameof(input.PresentedAmount));
            return View(input);
        }
        if (input.RequestId == Guid.Empty)
            return BadRequest();
        if (input.PresentedVersionId != input.Plan.Id || input.PresentedAmount != input.FinalAmount)
        {
            ModelState.AddModelError("", "PlanChanged");
            input.PresentedVersionId = input.Plan.Id;
            input.PresentedAmount = input.FinalAmount;
            ModelState.Remove(nameof(input.PresentedVersionId));
            ModelState.Remove(nameof(input.PresentedAmount));
            return View(input);
        }
        if (!input.AcceptTerms)
            ModelState.AddModelError("", "AcceptTermsRequired");
        if (!ModelState.IsValid)
            return View(input);
        Guid orderId;
        await using (
            var transaction = await Db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            )
        )
        {
            var lockedMember = await membership.LockMemberAsync(MemberId, ct);
            if (!lockedMember.Activated || lockedMember.Suspended)
                return Forbid();
            var replay = await Db.PaymentOrders.SingleOrDefaultAsync(
                x => x.Id == input.RequestId,
                ct
            );
            if (replay is not null)
                return replay.UserId == MemberId
                    ? RedirectToAction("Details", "Payments", new { id = replay.Id })
                    : BadRequest();
            // Re-read the plan under the transaction: an edit cannot silently change the displayed terms.
            var current = await Db.MembershipPlans.SingleAsync(x => x.Id == input.PlanId, ct);
            if (!current.Visible || current.CurrentVersionId != input.Plan.Id)
            {
                ModelState.AddModelError("", "PlanChanged");
                return View(input);
            }
            DiscountCode? discount = null;
            if (!string.IsNullOrWhiteSpace(input.DiscountCode))
            {
                var code = input.DiscountCode.Trim().ToUpperInvariant();
                discount = await Db.DiscountCodes.SingleOrDefaultAsync(
                    x =>
                        x.Code == code
                        && x.PlanId == current.Id
                        && x.Enabled
                        && x.ExpiresAt > DateTimeOffset.UtcNow,
                    ct
                );
                // Reserve the discount when the order is created. A reservation remains attached
                // even for a late provider confirmation, avoiding oversubscription of code limits.
                if (
                    discount is null
                    || discount.MaximumUses
                        <= await Db.PaymentOrders.CountAsync(
                            x => x.DiscountCodeId == discount.Id,
                            ct
                        )
                    || await Db.PaymentOrders.AnyAsync(
                        x => x.DiscountCodeId == discount.Id && x.UserId == MemberId,
                        ct
                    )
                )
                {
                    ModelState.AddModelError("", "DiscountInvalid");
                    return View(input);
                }
            }
            var amount = MembershipRules.Amount(input.Plan.PriceIls, discount?.Percent ?? 0);
            if (amount > 0 && !(await registry.AvailableAsync(ct)).Any(x => x.Key == input.Gateway))
            {
                ModelState.AddModelError("", "GatewayUnavailable");
                return View(input);
            }
            var now = DateTimeOffset.UtcNow;
            var order = new PaymentOrder
            {
                Id = input.RequestId,
                UserId = MemberId,
                PlanVersionId = input.Plan.Id,
                AmountIls = amount,
                DiscountCodeId = discount?.Id,
                DiscountPercent = discount?.Percent ?? 0,
                Gateway = amount == 0 ? "discount" : input.Gateway,
                ExpiresAt = now.AddMinutes(10),
                TermsAcceptedAt = now,
            };
            Db.PaymentOrders.Add(order);
            await Db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            orderId = order.Id;
        }
        if (input.FinalAmount == 0)
            await membership.ConfirmAsync(
                orderId,
                MemberId,
                false,
                "Full discount",
                "Valid reserved discount",
                "discount:" + orderId,
                0,
                "ILS",
                ct
            );
        return RedirectToAction("Details", "Payments", new { id = orderId });
    }
}
