namespace HiringPs.Core;

/// <summary>حقائق الدفع اللازمة لتحديد الإجراءات المتاحة دون تمرير كيان قاعدة للسياسة.</summary>
public sealed record PaymentActionInput(PaymentState State, string Gateway, DateTimeOffset ExpiresAt,
    DateTimeOffset? CheckoutStartedAt, DateTimeOffset? LastCheckedAt);
