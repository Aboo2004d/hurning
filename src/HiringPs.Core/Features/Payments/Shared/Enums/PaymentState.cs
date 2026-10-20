namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـPaymentState؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum PaymentState
{
    AwaitingPayment = 0,
    Pending = 1,
    Failed = 2,
    Cancelled = 3,
    Paid = 4,
    ManuallyApproved = 5,
}
