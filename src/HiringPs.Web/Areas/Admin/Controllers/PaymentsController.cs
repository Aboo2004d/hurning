using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class PaymentsController(
    HiringDb db,
    MembershipService membership,
    PaymentReconciliation reconciliation,
    PaymentEvidenceStore evidenceStore,
    PaymentTraceWriter? diagnostics = null
) : Controller
{
    public async Task<IActionResult> Index(
        string? q,
        string? gateway,
        string? environment,
        PaymentState? state,
        bool review = false,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var query = db.PaymentOrders.AsNoTracking().Include(x => x.PlanVersion).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            Guid.TryParse(q, out var orderId);
            long.TryParse(q, out var transactionId);
            query = query.Where(x =>
                x.Id == orderId
                || x.ProviderReference == q
                || x.UserId == q
                || db.WalletPaymentSessions.Any(session =>
                    session.OrderId == x.Id
                    && (session.JawwalReference == q || session.TransactionId == transactionId)
                )
            );
        }
        if (!string.IsNullOrWhiteSpace(gateway))
            query = query.Where(x => x.Gateway == gateway);
        if (environment is "Sandbox" or "Production")
            query = query.Where(x => x.GatewayEnvironment == environment);
        if (state is not null)
            query = query.Where(x => x.State == state);
        if (review)
            query = query.Where(x =>
                db.PaymentReviews.Any(r => r.OrderId == x.Id && r.ResolvedAt == null)
            );
        return View(
            await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).PageAsync(page, ct)
        );
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await db
            .PaymentOrders.AsNoTracking()
            .Include(x => x.PlanVersion)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null)
            return NotFound();
        var traceActor = "Payment:" + id.ToString("N");
        ViewBag.PaymentTraces = await db
            .Audit.AsNoTracking()
            .Where(x => x.Action == "Payment.VerifyTrace" && x.Actor == traceActor)
            .OrderByDescending(x => x.At)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(ct);
        ViewBag.TraceHost = HttpContext
            .RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()
            .EnvironmentName;
        ViewBag.Review = await db
            .PaymentReviews.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == id, ct);
        ViewBag.WalletReceipt = await db
            .WalletPaymentSessions.AsNoTracking()
            .Where(x => x.OrderId == id)
            .Select(x => new WalletReceiptSummary(
                x.TransactionId,
                x.JawwalReference,
                x.ConfirmedAmount,
                x.JawwalFee,
                x.PlatformFee,
                x.NetCredited
            ))
            .SingleOrDefaultAsync(ct);
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> Check(Guid id, CancellationToken ct)
    {
        TempData["Notice"] = await reconciliation.CheckAsync(
            id,
            null,
            User.Identity?.Name ?? "Admin",
            ct
        );
        TempData["PaymentTraceId"] = diagnostics?.LastTraceId;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(
        Guid id,
        string reason,
        string evidence,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(reason)
            || string.IsNullOrWhiteSpace(evidence)
            || reason.Length > 1500
            || evidence.Length > 1500
        )
            return BadRequest();
        if (
            !await db.PaymentOrders.AnyAsync(
                x => x.Id == id && x.GatewayEnvironment == db.ActivePaymentEnvironment,
                ct
            )
        )
        {
            TempData["Notice"] = "PaymentEnvironmentMismatch";
            return RedirectToAction(nameof(Details), new { id });
        }
        await membership.ConfirmAsync(
            id,
            User.Identity?.Name ?? "Admin",
            true,
            reason ?? "",
            evidence ?? "",
            null,
            null,
            null,
            ct
        );
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(Guid id, string result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result) || result.Length > 1500)
            return BadRequest();
        var review = await db.PaymentReviews.SingleOrDefaultAsync(x => x.OrderId == id, ct);
        if (review is null)
            return NotFound();
        if (review.ResolvedAt is null)
        {
            review.ResolvedAt = DateTimeOffset.UtcNow;
            review.Resolution = result.Trim();
            var member = await db.MemberProfiles.SingleAsync(x => x.UserId == review.UserId, ct);
            db.MemberMessages.Add(
                new MemberMessage
                {
                    UserId = review.UserId,
                    EventKey = "review:" + review.Id,
                    Kind = MemberMessageKind.Review,
                    Text =
                        (
                            member.Language == "en"
                                ? "Payment review completed. Order: "
                                : "تمت معالجة مراجعة الدفع. الطلب: "
                        )
                        + id
                        + "\n"
                        + result,
                }
            );
            membership.Audit(
                User.Identity?.Name ?? "Admin",
                "Payment.ReviewResolve",
                $"Member {review.UserId}; order {id}; {result}"
            );
            await db.SaveChangesAsync(ct);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct)
    {
        var review = await db.PaymentReviews.SingleOrDefaultAsync(x => x.OrderId == id, ct);
        if (review?.AttachmentName is null)
            return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(
            evidenceStore.PathFor(review.AttachmentName),
            review.AttachmentName.EndsWith(".png") ? "image/png" : "image/jpeg",
            "payment-evidence" + Path.GetExtension(review.AttachmentName)
        );
    }
}
