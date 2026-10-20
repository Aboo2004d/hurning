using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class PaymentsController(
    HiringDb db,
    MembershipService membership,
    PaymentReconciliation reconciliation,
    PaymentEvidenceStore evidenceStore,
    PaymentTraceWriter? diagnostics = null,
    PaymentReviewService? reviewService = null,
    PaymentReadService? readService = null
) : Controller
{
    private readonly PaymentReviewService reviews = reviewService ?? new(db, membership);
    private readonly PaymentReadService payments = readService ?? new(db);
    public async Task<IActionResult> Index(
        string? q,
        string? gateway,
        string? environment,
        PaymentState? state,
        bool review = false,
        int page = 1,
        CancellationToken ct = default,
        bool unresolved = false
    )
    {
        var model = await payments.AdminOrders(new(q, gateway, environment, state, review, unresolved)).PageAsync(page, ct);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Payment-Page"] == "true"
            ? PartialView("_PaymentTable", model)
            : View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await payments.OrderAsync(id, null, ct);
        if (order is null) return NotFound();
        ViewBag.PaymentTraces = await payments.AdminTracesAsync(id, ct);
        ViewBag.TraceHost = HttpContext
            .RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()
            .EnvironmentName;
        ViewBag.Review = await payments.ReviewAsync(id, null, ct);
        ViewBag.WalletReceipt = await payments.WalletReceiptAsync(id, ct);
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
        var outcome = await reviews.ApproveAsync(new(id, User.Identity?.Name ?? "Admin", reason, evidence), ct);
        if (outcome.Outcome == PaymentReviewOutcome.InvalidRequest) return BadRequest();
        if (outcome.Notice is not null) TempData["Notice"] = outcome.Notice;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(Guid id, string result, CancellationToken ct)
    {
        var outcome = await reviews.ResolveAsync(new(id, User.Identity?.Name ?? "Admin", result), ct);
        if (outcome.Outcome == PaymentReviewOutcome.InvalidRequest) return BadRequest();
        if (outcome.Outcome == PaymentReviewOutcome.NotFound) return NotFound();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct)
    {
        var name = await reviews.EvidenceNameAsync(id, null, ct);
        if (name is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(
            evidenceStore.PathFor(name),
            name.EndsWith(".png") ? "image/png" : "image/jpeg",
            "payment-evidence" + Path.GetExtension(name)
        );
    }
}
