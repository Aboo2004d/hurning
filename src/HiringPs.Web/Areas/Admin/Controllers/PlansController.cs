using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class PlansController(HiringDb db, MembershipService membership) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var result = await db
            .MembershipPlans.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .PageAsync(page, ct);
        var ids = result.Items.Select(x => x.CurrentVersionId).ToArray();
        ViewBag.Versions = await db
            .MembershipPlanVersions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        return View(result);
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return View(new PlanInput());
        var plan = await db
            .MembershipPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (plan is null)
            return NotFound();
        var version = await db
            .MembershipPlanVersions.AsNoTracking()
            .SingleAsync(x => x.Id == plan.CurrentVersionId, ct);
        return View(
            new PlanInput
            {
                Id = id,
                NameAr = version.NameAr,
                NameEn = version.NameEn,
                DescriptionAr = version.DescriptionAr,
                DescriptionEn = version.DescriptionEn,
                BadgeAr = version.BadgeAr,
                BadgeEn = version.BadgeEn,
                PriceIls = version.PriceIls,
                Days = version.Days,
                Details = Enum.GetValues<OpportunityKind>()
                    .Where(x => MembershipRules.Includes(version.DetailKinds, x))
                    .ToArray(),
                Alerts = Enum.GetValues<OpportunityKind>()
                    .Where(x => MembershipRules.Includes(version.AlertKinds, x))
                    .ToArray(),
                Visible = plan.Visible,
                SortOrder = plan.SortOrder,
                Version = Convert.ToBase64String(plan.Version),
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PlanInput input, CancellationToken ct)
    {
        if (input.Details.Concat(input.Alerts).Any(x => !Enum.IsDefined(x)))
            return BadRequest();
        if (!ModelState.IsValid)
            return View(input);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var plan = input.Id is null
            ? new MembershipPlan()
            : await db.MembershipPlans.SingleAsync(x => x.Id == input.Id, ct);
        if (input.Id is null)
            db.MembershipPlans.Add(plan);
        else
        {
            try
            {
                db.Entry(plan).Property(x => x.Version).OriginalValue = Convert.FromBase64String(
                    input.Version ?? ""
                );
            }
            catch (FormatException)
            {
                return BadRequest();
            }
        }
        plan.Visible = input.Visible;
        plan.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);
        var version = new MembershipPlanVersion
        {
            PlanId = plan.Id,
            NameAr = input.NameAr.Trim(),
            NameEn = input.NameEn.Trim(),
            DescriptionAr = (input.DescriptionAr ?? "").Trim(),
            DescriptionEn = (input.DescriptionEn ?? "").Trim(),
            BadgeAr = (input.BadgeAr ?? "").Trim(),
            BadgeEn = (input.BadgeEn ?? "").Trim(),
            PriceIls = input.PriceIls,
            Days = input.Days,
            DetailKinds = input.Details.Aggregate(
                0,
                (mask, kind) => mask | MembershipRules.Bit(kind)
            ),
            AlertKinds = input.Alerts.Aggregate(
                0,
                (mask, kind) => mask | MembershipRules.Bit(kind)
            ),
        };
        db.MembershipPlanVersions.Add(version);
        await db.SaveChangesAsync(ct);
        plan.CurrentVersionId = version.Id;
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Plan.Version",
            $"Plan {plan.Id}; version {version.Id}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Access(CancellationToken ct) =>
        View(await db.OpportunityAccessRules.OrderBy(x => x.Kind).ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Access(OpportunityKind[] publicKinds, CancellationToken ct)
    {
        if (publicKinds.Any(x => !Enum.IsDefined(x)))
            return BadRequest();
        foreach (var rule in await db.OpportunityAccessRules.ToListAsync(ct))
            rule.PublicDetails = publicKinds.Contains(rule.Kind);
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Access.Update",
            string.Join(',', publicKinds)
        );
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Access));
    }
}
