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
        return View(
            new WalletCheckoutPage
            {
                Order = order,
                IsSandbox = environment.ShowDevelopmentHints,
                CanInitiate = session is null && available && order.ExpiresAt > now,
                CanConfirm =
                    gateway.Ready
                    && session
                        is { HasKey: true, ConfirmationStartedAt: null, InitiationRejected: false }
                    && session.ExpiresAt > now
                    && order.ExpiresAt > now,
                ConfirmationStarted = session?.ConfirmationStartedAt != null,
                Rejected =
                    session?.ConfirmationRejected == true
                    || session?.InitiationRejected == true
                    || order.State == PaymentState.Failed,
                CanRetryOrder =
                    session?.ConfirmationRejected == true
                    || order.State == PaymentState.Failed
                    || session?.ConfirmationStartedAt == null
                        && (session is not null || order.ExpiresAt <= now),
                PhoneHint = session is null ? null : "05••••" + session.Phone[^4..],
                ExpiresAt = session?.ExpiresAt,
            }
        );
    }

    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Initiate(Guid id, string? phone, CancellationToken ct)
    {
        var outcome = await checkout.InitiateAsync(id, MemberId, phone, ct);
        if (outcome == "PaymentNotFound")
            return NotFound();
        TempData["Notice"] = outcome;
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Confirm(Guid id, string? otp, CancellationToken ct)
    {
        ModelState.Clear();
        var outcome = await checkout.ConfirmAsync(id, MemberId, otp, ct);
        if (outcome == "PaymentNotFound")
            return NotFound();
        TempData["Notice"] = outcome;
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        return outcome == "PaymentConfirmed"
            ? RedirectToAction("Details", "Payments", new { id })
            : RedirectToAction(nameof(Index), new { id });
    }
}
