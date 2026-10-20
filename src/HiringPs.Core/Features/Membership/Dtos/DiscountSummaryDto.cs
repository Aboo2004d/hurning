namespace HiringPs.Core;

/// <summary>بيانات كود الخصم للعرض دون كيان قابل للتتبع.</summary>
public sealed record DiscountSummaryDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public int? PlanId { get; init; }
    public int Percent { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int MaximumUses { get; init; }
    public bool Enabled { get; init; } = true;
}
