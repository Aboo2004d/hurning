using System.Security.Claims;
using HiringPs.Core;
using HiringPs.Web.Areas.Member.Controllers;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Optional integration coverage in MembershipSqlChecks' disposable SQL database.
// The gateway is an in-memory stub: no charge, SMS or external request is possible.
public static class CheckoutSqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options, int versionId)
    {
        void Check(bool ok, string label)
        {
            if (!ok)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        var owner = Guid.NewGuid().ToString();
        int planId;
        await using (var db = new HiringDb(options))
        {
            planId = (await db.MembershipPlanVersions.SingleAsync(x => x.Id == versionId)).PlanId;
            db.Users.Add(new IdentityUser { Id = owner, UserName = owner + "@invalid.test" });
            db.MemberProfiles.Add(
                new MemberProfile
                {
                    UserId = owner,
                    Name = "Checkout fixture",
                    Activated = true,
                }
            );
            await db
                .PaymentGatewaySettings.Where(x => x.Key == "Lahza")
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));
            var otherPlan = new MembershipPlan();
            db.MembershipPlans.Add(otherPlan);
            await db.SaveChangesAsync();
            foreach (
                var (code, percent, enabled, expired, uses, scope) in new[]
                {
                    ("CHECKOUT20", 20, true, false, 10, planId),
                    ("CHECKOUTFREE", 100, true, false, 10, planId),
                    ("CHECKOUTOFF", 20, false, false, 10, planId),
                    ("CHECKOUTOLD", 20, true, true, 10, planId),
                    ("CHECKOUTFULL", 20, true, false, 1, planId),
                    ("CHECKOUTOTHER", 20, true, false, 10, otherPlan.Id),
                }
            )
                db.DiscountCodes.Add(
                    new DiscountCode
                    {
                        Code = code,
                        Percent = percent,
                        Enabled = enabled,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1),
                        MaximumUses = uses,
                        PlanId = scope,
                    }
                );
            await db.SaveChangesAsync();
        }
        await using (var db = new HiringDb(options))
        {
            var otherOwner = Guid.NewGuid().ToString();
            db.Users.Add(
                new IdentityUser { Id = otherOwner, UserName = otherOwner + "@invalid.test" }
            );
            var full = await db.DiscountCodes.SingleAsync(x => x.Code == "CHECKOUTFULL");
            db.PaymentOrders.Add(
                new PaymentOrder
                {
                    UserId = otherOwner,
                    PlanVersionId = versionId,
                    Gateway = "Lahza",
                    AmountIls = 4,
                    DiscountCodeId = full.Id,
                    DiscountPercent = 20,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                }
            );
            await db.SaveChangesAsync();
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Payments:Lahza:Enabled"] = "true" }
            )
            .Build();
        async Task<(PurchaseInput Input, IActionResult Result, bool Invalid)> Preview(
            string code,
            bool checkout = false,
            Guid? requestId = null
        )
        {
            await using var db = new HiringDb(options);
            var membership = new MembershipService(db);
            var registry = new PaymentGatewayRegistry(db, configuration, [new NoChargeGateway()]);
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner)], "Fixture")
                ),
            };
            http.Request.Headers["X-Checkout-Async"] = "true";
            var controller = new PlansController(db, registry, membership)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                Url = new FixtureUrls(),
            };
            var input = new PurchaseInput
            {
                PlanId = planId,
                DiscountCode = code,
                Gateway = "Lahza",
                AcceptTerms = true,
                PresentedAmount = 4,
                PresentedVersionId = versionId,
                RequestId = requestId ?? Guid.NewGuid(),
            };
            var result = await controller.Review(input, checkout);
            return (input, result, !controller.ModelState.IsValid);
        }
        var valid = await Preview(" checkout20 ");
        Check(
            valid.Input.FinalAmount == 4 && !valid.Invalid && valid.Result is PartialViewResult,
            "Discount preview normalizes code and returns server-priced partial without checkout"
        );
        var free = await Preview("CHECKOUTFREE");
        Check(
            free.Input.FinalAmount == 0 && free.Input.Gateways.Length == 0,
            "Full discount needs no gateway"
        );
        foreach (
            var code in new[]
            {
                "MISSING",
                "CHECKOUTOFF",
                "CHECKOUTOLD",
                "CHECKOUTFULL",
                "CHECKOUTOTHER",
            }
        )
        {
            var rejected = await Preview(code);
            Check(
                rejected.Invalid && rejected.Input.FinalAmount == 5,
                "Preview rejects unavailable discount before showing a discounted total: " + code
            );
        }
        await using (var db = new HiringDb(options))
            Check(
                !await db.PaymentOrders.AnyAsync(x => x.UserId == owner),
                "Preview does not reserve codes or create orders"
            );
        var request = Guid.NewGuid();
        var purchased = await Preview("CHECKOUT20", checkout: true, requestId: request);
        Check(
            purchased.Result is JsonResult && !purchased.Invalid,
            "Checkout returns an internal destination after committing the reservation"
        );
        var used = await Preview("CHECKOUT20");
        Check(
            used.Invalid && used.Input.FinalAmount == 5,
            "Same member cannot reuse a reserved discount"
        );
        var replay = await Preview("CHECKOUT20", checkout: true, requestId: request);
        Check(
            replay.Result is JsonResult,
            "A checkout replay navigates to its existing order despite the discount reservation"
        );
        await using (var db = new HiringDb(options))
            Check(
                await db.PaymentOrders.CountAsync(x => x.UserId == owner) == 1
                    && (await db.PaymentOrders.SingleAsync(x => x.Id == request)).AmountIls == 4,
                "Request replay cannot create a second order or change its charged amount"
            );
    }

    private sealed class NoChargeGateway : IPaymentGateway
    {
        public string Key => "Lahza";
        public bool Ready => true;

        public Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct) =>
            throw new Exception("No provider request permitted in checkout checks");

        public Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct) =>
            throw new Exception("No provider request permitted in checkout checks");
    }

    private sealed class FixtureUrls : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext context) => "/en/Member/Payments/Details";

        public string? Content(string? path) => path;

        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true;

        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext context) => null;
    }
}
