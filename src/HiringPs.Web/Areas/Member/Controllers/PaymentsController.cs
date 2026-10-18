using HiringPs.Core;
using HiringPs.Web.Infrastructure;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PaymentsController(
    HiringDb db,
    PaymentGatewayRegistry registry,
    PaymentReconciliation reconciliation,
    PaymentEvidenceStore evidenceStore
) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await Db
                .PaymentOrders.AsNoTracking()
                .Include(x => x.PlanVersion)
                .Where(x => x.UserId == MemberId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .PageAsync(page, ct)
        );

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await Db
            .PaymentOrders.AsNoTracking()
            .Include(x => x.PlanVersion)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == MemberId, ct);
        if (order is null)
            return NotFound();
        ViewBag.Review = await Db
            .PaymentReviews.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == id, ct);
        ViewBag.Ready = (await registry.AvailableAsync(ct)).Any(x => x.Key == order.Gateway);
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        var order = await Db.PaymentOrders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == MemberId,
            ct
        );
        if (order is null)
            return NotFound();
        if (MembershipRules.Paid(order.State) || order.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            TempData["Notice"] = "PaymentExpired";
            return RedirectToAction(nameof(Details), new { id });
        }
        var gateway = (await registry.AvailableAsync(ct)).SingleOrDefault(x =>
            x.Key == order.Gateway
        );
        if (gateway is null)
        {
            TempData["Notice"] = "GatewayUnavailable";
            return RedirectToAction(nameof(Details), new { id });
        }
        var checkout = await gateway.CheckoutAsync(order, ct);
        if (checkout.Url.Scheme != "https")
            throw new InvalidOperationException("Invalid checkout URL");
        order.ProviderReference = checkout.Reference;
        order.State = PaymentState.Pending;
        await Db.SaveChangesAsync(ct);
        return Redirect(checkout.Url.AbsoluteUri);
    }

    [HttpPost]
    public async Task<IActionResult> Check(Guid id, CancellationToken ct)
    {
        TempData["Notice"] = await reconciliation.CheckAsync(id, MemberId, MemberId, ct);
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
        if (!await Db.PaymentOrders.AnyAsync(x => x.Id == id && x.UserId == MemberId, ct))
            return NotFound();
        if (note?.Length > 2000)
            return BadRequest();
        if (await Db.PaymentReviews.AnyAsync(x => x.OrderId == id, ct))
            return RedirectToAction(nameof(Details), new { id });
        try
        {
            var name = await evidenceStore.SaveAsync(evidence, ct);
            Db.PaymentReviews.Add(
                new PaymentReview
                {
                    OrderId = id,
                    UserId = MemberId,
                    Note = note?.Trim() ?? "",
                    AttachmentName = name,
                }
            );
            await Db.SaveChangesAsync(ct);
            TempData["Notice"] = "ReviewRequested";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Notice"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct)
    {
        var review = await Db.PaymentReviews.SingleOrDefaultAsync(
            x => x.OrderId == id && x.UserId == MemberId,
            ct
        );
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
