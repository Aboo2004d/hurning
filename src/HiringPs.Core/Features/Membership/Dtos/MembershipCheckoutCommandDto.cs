namespace HiringPs.Core;

/// <summary>طلب إنشاء جلسة اشتراك يربط نسخة الشروط والمبلغ المعروض والعضو ومفتاح منع التكرار.</summary>
public sealed record MembershipCheckoutCommandDto(Guid RequestId, string Owner, int PlanId,
    int PresentedVersionId, decimal FinalAmount, string DiscountCode, string Gateway);
