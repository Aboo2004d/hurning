namespace HiringPs.Core;
/// <summary>نتيجة موعد نشر عشوائي؛ المحاولات المحجوزة أو غير المحسومة لا تعاد تلقائيًا.</summary>
public enum RandomPublicationState { Reserved = 0, Published = 1, Failed = 2, Skipped = 3, Unknown = 4 }
