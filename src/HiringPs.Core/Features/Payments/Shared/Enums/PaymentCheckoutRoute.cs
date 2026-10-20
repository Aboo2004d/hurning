namespace HiringPs.Core;

/// <summary>وجهة واجهة المستخدم بعد محاولة بدء الدفع؛ ليست حالة اعتماد دفعة.</summary>
public enum PaymentCheckoutRoute
{
    NotFound = 0,
    InvalidRequest = 1,
    Details = 2,
    Wallet = 3,
    External = 4,
}
