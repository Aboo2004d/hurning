using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحفظ شروط الباقات في نسخ مستقلة مع تحقق التزامن وتدقيق التعديل.</summary>
public sealed class PlanManagementService(HiringDb db, MembershipService membership)
{
    /// <summary>ينشئ نسخة جديدة ويحدث المؤشر الحالي ذريًا؛ المشتريات القائمة لا تتغير.</summary>
    public async Task<PlanSaveResultDto> SaveAsync(PlanSaveCommandDto input, CancellationToken ct)
    {
        if (input.MaxPreferredCategories is < 1 or > 1000 || input.Id is <= 0 || input.PriceIls is < 0.01m or > 100000m || input.Days is < 1 or > 3650
            || string.IsNullOrWhiteSpace(input.NameAr) || string.IsNullOrWhiteSpace(input.NameEn)
            || input.NameAr.Length > 160 || input.NameEn.Length > 160
            || input.DescriptionAr.Length > 2000 || input.DescriptionEn.Length > 2000
            || input.BadgeAr.Length > 80 || input.BadgeEn.Length > 80
            || input.Details.Concat(input.Alerts).Any(x => !Enum.IsDefined(x)))
            return new(PlanSaveOutcome.InvalidRequest);
        try { return await SaveCoreAsync(input, ct); }
        catch (DbUpdateConcurrencyException) { return new(PlanSaveOutcome.Conflict); }
    }

    /// <summary>ينفذ المعاملة ويثبت نسخة الشروط قبل إعادة نجاح الحفظ للمستدعي.</summary>
    private async Task<PlanSaveResultDto> SaveCoreAsync(PlanSaveCommandDto input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var plan = input.Id is null
            ? new MembershipPlan()
            : await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == input.Id, ct);
        if (plan is null)
            return new(PlanSaveOutcome.NotFound);
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
                return new(PlanSaveOutcome.InvalidRequest);
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
            MaxPreferredCategories = input.MaxPreferredCategories,
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
            input.Actor,
            "Plan.Version",
            $"Plan {plan.Id}; version {version.Id}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(PlanSaveOutcome.Saved, plan.Id);
    }
}
