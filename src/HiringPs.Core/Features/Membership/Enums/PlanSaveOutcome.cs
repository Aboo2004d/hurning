namespace HiringPs.Core;

/// <summary>نتيجة حفظ نسخة الباقة بما فيها تعارض التعديلات المتزامنة.</summary>
public enum PlanSaveOutcome { Saved = 0, InvalidRequest = 1, NotFound = 2, Conflict = 3 }
