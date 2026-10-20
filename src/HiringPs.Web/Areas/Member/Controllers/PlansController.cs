using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PlansController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    MembershipService membership,
    MemberPlanReadService? readService = null,
    MembershipDiscountService? discountService = null,
    MembershipCheckoutService? checkoutService = null
) : MemberControllerBase(db)
{
    private MembershipDiscountService Discounts => discountService ?? new MembershipDiscountService(Db);

    /// <summary>يعرض الباقات المرئية وفترات استحقاق العضو عبر عقود قراءة مستقلة.</summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var reader = readService ?? new MemberPlanReadService(Db);
        return View(new PlanCatalog
        {
            Plans = await reader.CatalogAsync(ct),
            Subscriptions = await reader.PeriodsAsync(MemberId, ct),
        });
    }

    /// <summary>يعيد كتالوج العرض الحالي فقط؛ لا يحدث جلسات الدفع أو فترات الاستحقاق السابقة.</summary>
    [HttpGet]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var reader = readService ?? new MemberPlanReadService(Db);
        return PartialView("_Catalog", await reader.CatalogAsync(ct));
    }

    /// <summary>يحدث معاينة ما قبل الشراء فقط دون إنشاء طلب أو تأكيد دفع أو استهلاك خصم.</summary>
    [HttpGet]
    public async Task<IActionResult> Quote(int id, string? code, string? gateway, Guid requestId, CancellationToken ct)
    {
        if (requestId == Guid.Empty || code?.Length > 50) return BadRequest();
        if (await Db.PaymentOrders.AnyAsync(x => x.Id == requestId && x.UserId == MemberId
            && x.GatewayEnvironment == Db.ActivePaymentEnvironment, ct))
            return NoContent(); // This key already belongs to a frozen payment session.
        var input = new PurchaseInput
        {
            PlanId = id, RequestId = requestId, DiscountCode = code ?? "", Gateway = gateway ?? "",
        };
        await FillAsync(input, ct, chooseGateway: false);
        if (input.Plan is null) return NotFound();
        input.PresentedVersionId = input.Plan.Id;
        input.PresentedAmount = input.FinalAmount;
        Response.Headers.CacheControl = "no-store";
        return PartialView("_CheckoutReview", input);
    }

    /// <summary>يجهز مراجعة باقة ويثبت النسخة والمبلغ اللذين سيوافق عليهما العضو.</summary>
    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        var input = new PurchaseInput { PlanId = id };
        await FillAsync(input, ct);
        input.PresentedVersionId = input.Plan?.Id ?? 0;
        input.PresentedAmount = input.FinalAmount;
        return input.Plan is null ? NotFound() : ReviewResult(input);
    }

    /// <summary>يعيد بيانات الباقة والخصم والبوابات من الخادم قبل العرض أو بدء الشراء.</summary>
    private async Task FillAsync(
        PurchaseInput input,
        CancellationToken ct,
        bool chooseGateway = true
    )
    {
        var reader = readService ?? new MemberPlanReadService(Db);
        input.Plan = await reader.CurrentAsync(input.PlanId, ct);
        input.FinalAmount = input.Plan?.PriceIls ?? 0;
        input.StartsAt = await reader.StartsAsync(MemberId, DateTimeOffset.UtcNow, ct);
        if (input.Plan is not null && !string.IsNullOrWhiteSpace(input.DiscountCode))
        {
            var discount = await Discounts.FindAsync(input.DiscountCode, input.Plan.PlanId, MemberId, ct);
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

    /// <summary>يتحقق من الموافقة والشروط المعروضة ثم يفوض حجز طلب الاشتراك إلى الخدمة.</summary>
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
        var service = checkoutService ?? new MembershipCheckoutService(Db, membership, registry, Discounts);
        var result = await service.CreateAsync(new MembershipCheckoutCommandDto(input.RequestId,
            MemberId, input.PlanId, input.Plan.Id, input.FinalAmount, input.DiscountCode, input.Gateway), ct);
        if (result.Outcome == MembershipCheckoutOutcome.Forbidden) return Forbid();
        if (result.Outcome == MembershipCheckoutOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == MembershipCheckoutOutcome.Completed && result.OrderId is { } orderId)
            return PaymentDestination(orderId);
        if (result.Outcome == MembershipCheckoutOutcome.PlanChanged)
        {
            await FillAsync(input, ct, chooseGateway: false);
            if (input.Plan is null) return NotFound();
            input.PresentedVersionId = input.Plan.Id;
            input.PresentedAmount = input.FinalAmount;
            ModelState.Remove(nameof(input.PresentedVersionId));
            ModelState.Remove(nameof(input.PresentedAmount));
        }
        ModelState.AddModelError("", result.Outcome.ToString());
        return ReviewResult(input);
    }

    /// <summary>يعيد مراجعة الشراء كاملة أو جزئية مع منع تخزينها في المتصفح.</summary>
    private IActionResult ReviewResult(PurchaseInput input)
    {
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Checkout-Async"] == "true"
            ? PartialView("_CheckoutReview", input)
            : View("Review", input);
    }

    /// <summary>يوجه إلى جلسة الدفع القائمة بالطريقة الملائمة للطلب العادي أو غير المتزامن.</summary>
    private IActionResult PaymentDestination(Guid id) =>
        Request.Headers["X-Checkout-Async"] == "true"
            ? Json(new { redirectUrl = Url.Action("Details", "Payments", new { id }) })
            : RedirectToAction("Details", "Payments", new { id });
}
