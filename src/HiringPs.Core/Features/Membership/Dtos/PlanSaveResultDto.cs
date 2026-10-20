namespace HiringPs.Core;

/// <summary>نتيجة حفظ الباقة؛ لا يصدر إشعار تحديث الكتالوج إلا بعد Saved.</summary>
public sealed record PlanSaveResultDto(PlanSaveOutcome Outcome, int? PlanId = null);
