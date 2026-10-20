namespace HiringPs.Core;

/// <summary>تفاصيل تشخيص دفع مسموحة للعرض الإداري دون تمرير كيان سجل التدقيق.</summary>
public sealed record PaymentTraceSummaryDto
{
    public DateTimeOffset At { get; init; }
    public string Detail { get; init; } = "";
}
