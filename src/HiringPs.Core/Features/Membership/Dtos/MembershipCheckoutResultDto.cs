namespace HiringPs.Core;

/// <summary>نتيجة إنشاء طلب الاشتراك ومعرّف الطلب عند نجاحه أو إعادة استخدامه.</summary>
public sealed record MembershipCheckoutResultDto(MembershipCheckoutOutcome Outcome, Guid? OrderId = null);
