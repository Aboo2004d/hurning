namespace HiringPs.Core;

/// <summary>إسقاط للقراءة لصفحات الدفع؛ لا يحتوي كيان EF أو نسخة التزامن أو حقول جلسة المحفظة السرية.</summary>
public sealed record PaymentOrderSummaryDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = "";
    public decimal AmountIls { get; init; }
    public string Gateway { get; init; } = "";
    public string GatewayEnvironment { get; init; } = "";
    public string? ProviderReference { get; init; }
    public PaymentState State { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? CheckoutStartedAt { get; init; }
    public DateTimeOffset? LastCheckedAt { get; init; }
    public string DecisionReason { get; init; } = "";
    public string PaymentEvidence { get; init; } = "";
    public bool NeedsReconciliation { get; init; }
    public PaymentPlanSummaryDto PlanVersion { get; init; } = new();
}
