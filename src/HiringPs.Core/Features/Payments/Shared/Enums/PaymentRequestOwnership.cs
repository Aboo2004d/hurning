namespace HiringPs.Core;

/// <summary>نتيجة قراءة مفتاح طلب الشراء؛ ليست حالة دفع ولا تمنح استحقاقًا.</summary>
public enum PaymentRequestOwnership
{
    Missing = 0,
    Owned = 1,
    OtherOwnerOrEnvironment = 2,
}
