using HiringPs.Web.Services.Payments;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public sealed class WalletPaymentsController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    JawwalPayGateway gateway,
    WalletCheckoutService checkout,
    PaymentEnvironment environment,
    PaymentTraceWriter? diagnostics = null,
    PaymentReadService? readService = null,
    WalletPaymentPageService? pageService = null
) : MemberControllerBase(db)
{
    private readonly WalletPaymentPageService walletPage = pageService ?? new(readService ?? new(db), registry, gateway, environment);
    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken ct)
    {
        var page = await walletPage.ReadAsync(id, MemberId, Db.ActivePaymentEnvironment, ct);
        if (page is null) return NotFound();
        if (MembershipRules.Paid(page.Order.State))
            return RedirectToAction("Details", "Payments", new { id });
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Checkout-Async"] == "true"
            ? PartialView("_WalletCheckout", page)
            : View(page);
    }

    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Initiate(Guid id, string? phone, CancellationToken ct)
    {
        var outcome = await checkout.InitiateAsync(id, MemberId, phone, ct);
        return await ReplyAsync(id, outcome, ct);
    }

    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Confirm(Guid id, string? otp, CancellationToken ct)
    {
        ModelState.Clear();
        var outcome = await checkout.ConfirmAsync(id, MemberId, otp, ct);
        return await ReplyAsync(id, outcome, ct);
    }

    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var outcome = await checkout.CancelAsync(id, MemberId, ct);
        if (outcome == "WalletCancelled")
        {
            TempData["Notice"] = outcome;
            return Navigate("Index", "Plans", null);
        }
        return await ReplyAsync(id, outcome, ct);
    }

    private async Task<IActionResult> ReplyAsync(Guid id, string outcome, CancellationToken ct)
    {
        if (outcome == "PaymentNotFound")
            return NotFound();
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        if (outcome == "PaymentConfirmed")
        {
            TempData["Notice"] = outcome;
            return Navigate("Details", "Payments", id);
        }
        if (Request.Headers["X-Checkout-Async"] == "true")
            ViewData["CheckoutNotice"] = outcome;
        else
            TempData["Notice"] = outcome;
        return await Index(id, ct);
    }

    private IActionResult Navigate(string action, string controller, Guid? id) =>
        Request.Headers["X-Checkout-Async"] == "true"
            ? Json(new { redirectUrl = Url.Action(action, controller, new { id }) })
            : RedirectToAction(action, controller, new { id });
}
