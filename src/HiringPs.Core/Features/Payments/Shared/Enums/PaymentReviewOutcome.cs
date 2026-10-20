namespace HiringPs.Core;

/// <summary>نتيجة خدمة مراجعات الدفع، منفصلة عن رموز رسائل العرض وحالات الدفعة.</summary>
public enum PaymentReviewOutcome
{
    Completed = 0,
    NotFound = 1,
    InvalidRequest = 2,
    EnvironmentMismatch = 3,
    AlreadyExists = 4,
    Rejected = 5,
}
