namespace HiringPs.Core;

/// <summary>نتيجة حفظ اسم جهة بديل دون ربطها بلغة الواجهة.</summary>
public enum OrganizationAliasSaveOutcome { Added = 0, Empty = 1, SameName = 2, AlreadyExists = 3, ChainNotAllowed = 4, SaveFailed = 5 }
