namespace HiringPs.Core;

/// <summary>نتيجة مراجعة الدفع ورمز إشعار اختياري دون تفاصيل ملف مرفق أو استجابة مزود.</summary>
public sealed record PaymentReviewResult(PaymentReviewOutcome Outcome, string? Notice = null);
