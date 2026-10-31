namespace HiringPs.Core;

/// <summary>نتيجة تصنيف مقيد بالقائمة مع سبب المراجعة عند تعذر الاستنتاج.</summary>
public sealed record CategoryClassificationDto(string Category, string ReviewReason);
