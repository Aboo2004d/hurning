using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class JawwalPayController(IConfiguration configuration, JawwalPayGateway gateway)
    : Controller
{
    private JawwalPaySetup Setup()
    {
        var setup = JawwalPaySetup.FromConfiguration(configuration);
        setup.Ready = gateway.Ready;
        setup.IsSandbox = gateway.Sandbox;
        return setup;
    }

    [HttpGet]
    public IActionResult Index() => View(Setup());

    // An explicit, authenticated administrator action; never used to verify a payment.
    [HttpPost]
    public async Task<IActionResult> Balance(CancellationToken ct)
    {
        var setup = Setup();
        try
        {
            setup.Balance = await gateway.BalanceAsync(ct);
            setup.BalanceFetchedAt = DateTimeOffset.UtcNow;
        }
        catch (PaymentProviderException)
        {
            ModelState.AddModelError("", "GatewayUnavailable");
        }
        return View("Index", setup);
    }
}
