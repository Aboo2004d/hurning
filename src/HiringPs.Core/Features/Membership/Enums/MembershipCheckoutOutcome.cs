namespace HiringPs.Core;

/// <summary>نتيجة إنشاء طلب الاشتراك قبل تحويل المستخدم إلى الدفع.</summary>
public enum MembershipCheckoutOutcome
{
    Completed = 0,
    InvalidRequest = 1,
    Forbidden = 2,
    PlanChanged = 3,
    DiscountInvalid = 4,
    GatewayUnavailable = 5,
}
