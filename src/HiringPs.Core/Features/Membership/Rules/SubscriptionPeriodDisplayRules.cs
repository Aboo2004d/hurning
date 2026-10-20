namespace HiringPs.Core;

/// <summary>يجمع الفترات المتصلة للعرض دون تعديل سجلات الاستحقاق أو مدخلات المستدعي.</summary>
public static class SubscriptionPeriodDisplayRules
{
    /// <summary>يدمج فترتين متجاورتين فقط عندما ترتبطان بنسخة الباقة نفسها.</summary>
    public static List<MemberSubscriptionPeriodDto> Combine(IEnumerable<MemberSubscriptionPeriodDto> periods)
    {
        var result = new List<MemberSubscriptionPeriodDto>();
        foreach (var period in periods.OrderBy(x => x.StartsAt))
        {
            if (result.LastOrDefault() is { } previous
                && previous.PlanVersionId == period.PlanVersionId && previous.EndsAt == period.StartsAt)
                result[^1] = previous with { EndsAt = period.EndsAt };
            else result.Add(period);
        }
        return result;
    }
}
