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
        return input.Plan is null ? NotFound() : ReviewResult(input);
    }

    private async Task FillAsync(
        PurchaseInput input,
        CancellationToken ct,
        bool chooseGateway = true
    )
    {
        var plan = await Db
            .MembershipPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.PlanId && x.Visible, ct);
        input.Plan = plan is null
            ? null
            : await Db
                .MembershipPlanVersions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == plan.CurrentVersionId, ct);
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
            var discount = await DiscountAsync(input.DiscountCode, input.Plan.PlanId, ct);
            if (discount is null)
                ModelState.AddModelError(nameof(input.DiscountCode), "DiscountInvalid");
            else
                input.FinalAmount = MembershipRules.Amount(input.Plan.PriceIls, discount.Percent);
        }
        input.Gateways = (await registry.AvailableAsync(ct))
            .Where(x => x.SupportsAmount(input.FinalAmount))
            .Select(x => x.Key)
            .ToArray();
        if (chooseGateway && !input.Gateways.Contains(input.Gateway))
        {
            input.Gateway = input.Gateways.FirstOrDefault() ?? "";
            ModelState.Remove(nameof(input.Gateway));
        }
    }

    // Preview and purchase share all eligibility conditions; checkout runs under the existing serializable lock.
    private async Task<DiscountCode?> DiscountAsync(string value, int planId, CancellationToken ct)
    {
        var code = value.Trim().ToUpperInvariant();
        var discount = await Db
            .DiscountCodes.AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Code == code
                    && (x.PlanId == null || x.PlanId == planId)
                    && x.Enabled
                    && x.ExpiresAt > DateTimeOffset.UtcNow,
                ct
            );
        if (discount is null)
            return null;
        var reservations = Db.PaymentOrders.Where(x =>
            x.DiscountCodeId == discount.Id && x.GatewayEnvironment == Db.ActivePaymentEnvironment
        );
        if (
            await reservations.CountAsync(ct) >= discount.MaximumUses
            || await reservations.AnyAsync(x => x.UserId == MemberId, ct)
        )
            return null;
        return discount;
    }

    [HttpPost]
    public async Task<IActionResult> Review(
        PurchaseInput input,
        bool checkout = false,
        CancellationToken ct = default
    )
    {
        // A completed request remains replayable even after its discount was reserved.
        if (checkout && input.RequestId != Guid.Empty)
        {
            var replay = await Db
                .PaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == input.RequestId, ct);
            if (replay is not null)
                return
                    replay.UserId == MemberId
                    && replay.GatewayEnvironment == Db.ActivePaymentEnvironment
                    ? PaymentDestination(replay.Id)
                    : BadRequest();
        }
        await FillAsync(input, ct, chooseGateway: !checkout);
        if (input.Plan is null)
            return NotFound();
        if (!checkout)
        {
            input.PresentedVersionId = input.Plan.Id;
            input.PresentedAmount = input.FinalAmount;
            ModelState.Remove(nameof(input.PresentedVersionId));
            ModelState.Remove(nameof(input.PresentedAmount));
            Response.Headers.CacheControl = "no-store";
            return ReviewResult(input);
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
            return ReviewResult(input);
        }
        if (!input.AcceptTerms)
            ModelState.AddModelError("", "AcceptTermsRequired");
        if (!ModelState.IsValid)
            return ReviewResult(input);
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
                return replay.UserId == MemberId ? PaymentDestination(replay.Id) : BadRequest();
            // Re-read the plan under the transaction: an edit cannot silently change the displayed terms.
            var current = await Db.MembershipPlans.SingleAsync(x => x.Id == input.PlanId, ct);
            if (!current.Visible || current.CurrentVersionId != input.Plan.Id)
            {
                ModelState.AddModelError("", "PlanChanged");
                return ReviewResult(input);
            }
            DiscountCode? discount = null;
            if (!string.IsNullOrWhiteSpace(input.DiscountCode))
            {
                discount = await DiscountAsync(input.DiscountCode, current.Id, ct);
                // The reservation remains attached even after expiration/cancellation, including late verification.
                if (discount is null)
                {
                    ModelState.AddModelError("", "DiscountInvalid");
                    return ReviewResult(input);
                }
            }
            var amount = MembershipRules.Amount(input.Plan.PriceIls, discount?.Percent ?? 0);
            if (
                amount > 0
                && !(await registry.AvailableAsync(ct)).Any(x =>
                    x.Key == input.Gateway && x.SupportsAmount(amount)
                )
            )
            {
                ModelState.AddModelError("", "GatewayUnavailable");
                return ReviewResult(input);
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
                GatewayEnvironment = Db.ActivePaymentEnvironment,
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
        return PaymentDestination(orderId);
    }

    private IActionResult ReviewResult(PurchaseInput input)
    {
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Checkout-Async"] == "true"
            ? PartialView("_CheckoutReview", input)
            : View("Review", input);
    }

    private IActionResult PaymentDestination(Guid id) =>
        Request.Headers["X-Checkout-Async"] == "true"
            ? Json(new { redirectUrl = Url.Action("Details", "Payments", new { id }) })
            : RedirectToAction("Details", "Payments", new { id });
}
