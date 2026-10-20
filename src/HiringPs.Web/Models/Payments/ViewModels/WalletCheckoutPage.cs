using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class WalletCheckoutPage
{
    public required PaymentOrderSummaryDto Order { get; init; }
    public bool IsSandbox { get; init; }
    public bool CanInitiate { get; init; }
    public bool CanConfirm { get; init; }
    public bool ConfirmationStarted { get; init; }
    public bool Rejected { get; init; }
    public bool CanCancel { get; init; }
    public string? PhoneHint { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
