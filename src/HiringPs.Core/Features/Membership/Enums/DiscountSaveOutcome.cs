namespace HiringPs.Core;

/// <summary>نتائج حفظ كود الخصم وشروط عدم تعديل الخصومات المستخدمة.</summary>
public enum DiscountSaveOutcome { Saved = 0, InvalidRequest = 1, NotFound = 2, UsedImmutable = 3 }
