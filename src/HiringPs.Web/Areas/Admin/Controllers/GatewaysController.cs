using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class GatewaysController(
    HiringDb db,
    MembershipService membership,
    PaymentGatewayRegistry registry,
    IEnumerable<IPaymentGateway> adapters
) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Ready = adapters.Where(x => x.Ready).Select(x => x.Key).ToArray();
        ViewBag.Available = (await registry.AvailableAsync(ct)).Select(x => x.Key).ToArray();
        return View(await db.PaymentGatewaySettings.OrderBy(x => x.Key).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Update(string key, bool enabled, CancellationToken ct)
    {
        var setting = await db.PaymentGatewaySettings.SingleOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null)
            return NotFound();
        if (key == "JawwalPay" && enabled && !adapters.Any(x => x.Key == key && x.Ready))
        {
            TempData["Notice"] = "GatewayUnavailable";
            return RedirectToAction(nameof(Index));
        }
        setting.Enabled = enabled;
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Gateway.Update",
            $"Gateway {key}; enabled={enabled}"
        );
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
