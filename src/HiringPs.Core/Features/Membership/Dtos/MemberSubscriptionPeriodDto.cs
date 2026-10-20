namespace HiringPs.Core;

/// <summary>فترة استحقاق للعرض مرتبطة بنسخة الباقة المشتراة لا بنسخة السعر الحالية.</summary>
public sealed record MemberSubscriptionPeriodDto(int PlanVersionId,
    MembershipPlanVersionSummaryDto PlanVersion, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
