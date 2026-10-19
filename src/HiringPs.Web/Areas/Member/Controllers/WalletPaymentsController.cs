using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public sealed class WalletPaymentsController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    JawwalPayGateway gateway,
    WalletCheckoutService checkout,
    PaymentEnvironment environment,
    PaymentTraceWriter? diagnostics = null
) : MemberControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken ct)
    {
        var order = await Db
            .PaymentOrders.AsNoTracking()
            .Include(x => x.PlanVersion)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id
                    && x.UserId == MemberId
                    && x.Gateway == "JawwalPay"
                    && x.GatewayEnvironment == Db.ActivePaymentEnvironment,
                ct
            );
        if (order is null)
            return NotFound();
        if (MembershipRules.Paid(order.State))
            return RedirectToAction("Details", "Payments", new { id });
        // Never pass the protected session token (or a posted OTP) to a view.
        var session = await Db
            .WalletPaymentSessions.AsNoTracking()
            .Where(x => x.OrderId == id)
            .Select(x => new
            {
                x.Phone,
                HasKey = x.ProtectedSessionKey != null,
                x.ExpiresAt,
                x.ConfirmationStartedAt,
                x.InitiationRejected,
                x.ConfirmationRejected,
            })
            .SingleOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var available = (await registry.AvailableAsync(ct)).Any(x => x.Key == "JawwalPay");
        var page = new WalletCheckoutPage
        {
            Order = order,
            IsSandbox = environment.ShowDevelopmentHints,
            CanInitiate =
                session is null
                && available
                && order.ExpiresAt > now
                && order.State is PaymentState.AwaitingPayment or PaymentState.Pending,
            CanConfirm =
                gateway.Ready
                && session
                    is { HasKey: true, ConfirmationStartedAt: null, InitiationRejected: false }
                && session.ExpiresAt > now
                && order.ExpiresAt > now
                && order.State == PaymentState.Pending,
            CanCancel =
                !MembershipRules.Paid(order.State)
                && order.State != PaymentState.Cancelled
                && (session?.ConfirmationStartedAt is null || session.ConfirmationRejected),
            ConfirmationStarted = session?.ConfirmationStartedAt != null,
            Rejected =
                session?.ConfirmationRejected == true
                || session?.InitiationRejected == true
                || order.State == PaymentState.Failed,
            PhoneHint = session is null ? null : "05••••" + session.Phone[^4..],
            ExpiresAt = session?.ExpiresAt,
        };
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
