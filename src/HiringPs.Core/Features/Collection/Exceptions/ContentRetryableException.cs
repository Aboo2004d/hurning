namespace HiringPs.Core;

/// <summary>خطأ مؤقت في المصدر يعاد وفق التراجع المقرر بدل اعتباره إغلاقًا أو إرساله للمراجعة فورًا.</summary>
public class ContentRetryableException(string message) : Exception(message);
