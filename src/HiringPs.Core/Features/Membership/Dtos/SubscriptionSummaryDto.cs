namespace HiringPs.Core;

/// <summary>حقول عرض الاشتراك دون كيان النسخة أو rowversion أو تفاصيل طلب الدفع.</summary>
public sealed record SubscriptionSummaryDto
{
    public long Id { get; init; }
    public string UserId { get; init; } = "";
    public MembershipPlanLabelDto PlanVersion { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public bool Cancelled { get; init; }
}
