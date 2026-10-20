using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PaymentsController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    PaymentReconciliation reconciliation,
    PaymentEvidenceStore evidenceStore,
    PaymentTraceWriter? diagnostics = null,
    HostedPaymentCheckout? checkoutService = null,
    PaymentReviewService? reviewService = null,
    PaymentReadService? readService = null
) : MemberControllerBase(db)
{
    private readonly HostedPaymentCheckout hostedCheckout = checkoutService ?? new(db, registry);
    private readonly PaymentReviewService reviews = reviewService ?? new(db, new MembershipService(db));
    private readonly PaymentReadService payments = readService ?? new(db);
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var model = await payments.MemberOrders(MemberId).PageAsync(page, ct);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Payment-Page"] == "true"
            ? PartialView("_PaymentTable", model)
            : View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await payments.OrderAsync(id, MemberId, ct);
        if (order is null) return NotFound();
        ViewBag.Review = await payments.ReviewAsync(id, MemberId, ct);
        var ready = (await registry.AvailableAsync(ct)).Any(x => x.Key == order.Gateway);
        var session = await payments.WalletAsync(id, MemberId, ct);
        var returned = TempData.Peek("ReturnedPayment") as string == id.ToString("N");
        ViewBag.PaymentActions = PaymentActions.For(
            order,
            ready,
            session?.ConfirmationStartedAt is not null,
            session?.ConfirmationRejected == true,
            returned
        );
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Checkout-Async"] == "true"
            ? PartialView("_PaymentDetails", order)
            : View(order);
    }

    /// <summary>يترجم نتيجة خدمة بدء الدفع إلى استجابة ويب دون تنفيذ منطق البوابة داخل Controller.</summary>
    [HttpPost]
    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        var result = await hostedCheckout.StartAsync(id, MemberId, ct);
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        return result.Route switch
        {
            PaymentCheckoutRoute.NotFound => NotFound(),
            PaymentCheckoutRoute.InvalidRequest => BadRequest(),
            PaymentCheckoutRoute.Wallet => RedirectToAction("Index", "WalletPayments", new { id }),
            PaymentCheckoutRoute.External => Redirect(result.Url!),
            _ => RedirectToAction(nameof(Details), new { id })
        };
    }

    // The return URL is only navigation. Server-side verification determines payment.
    [HttpGet]
    public async Task<IActionResult> Returned(Guid id, CancellationToken ct)
    {
        TempData["ReturnedPayment"] = id.ToString("N");
        TempData["Notice"] = await reconciliation.CheckAsync(id, MemberId, MemberId, ct);
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Check(Guid id, CancellationToken ct)
    {
        var outcome = await reconciliation.CheckAsync(id, MemberId, MemberId, ct);
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        if (Request.Headers["X-Checkout-Async"] == "true")
        {
            ViewData["CheckoutNotice"] = outcome;
            return await Details(id, ct);
        }
        TempData["Notice"] = outcome;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Review(
        Guid id,
        string? note,
        IFormFile? evidence,
        CancellationToken ct
    )
    {
        var returned = TempData.Peek("ReturnedPayment") as string == id.ToString("N");
        var result = await reviews.RequestAsync(new(id, MemberId, note, returned),
            token => evidenceStore.SaveAsync(evidence, token), ct);
        if (result.Outcome == PaymentReviewOutcome.NotFound) return NotFound();
        if (result.Outcome == PaymentReviewOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == PaymentReviewOutcome.AlreadyExists)
            return Request.Headers["X-Checkout-Async"] == "true"
                ? await Details(id, ct) : RedirectToAction(nameof(Details), new { id });
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        if (Request.Headers["X-Checkout-Async"] == "true")
        {
            ViewData["CheckoutNotice"] = TempData["Notice"];
            return await Details(id, ct);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct)
    {
        var name = await reviews.EvidenceNameAsync(id, MemberId, ct);
        if (name is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(
            evidenceStore.PathFor(name),
            name.EndsWith(".png") ? "image/png" : "image/jpeg",
            "payment-evidence" + Path.GetExtension(name)
        );
    }
}
