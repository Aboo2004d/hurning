namespace HiringPs.Core;

/// <summary>بيانات الباقة اللازمة لعرض طلب الدفع دون كيان قابل للتتبع أو حقول صلاحيات الاشتراك.</summary>
public sealed record PaymentPlanSummaryDto
{
    public int PlanId { get; init; }
    public string NameAr { get; init; } = "";
    public string NameEn { get; init; } = "";
    public int Days { get; init; }

    /// <summary>يختار الاسم المحفوظ في نسخة الباقة بلغة العرض الحالية.</summary>
    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
