namespace HiringPs.Core;

/// <summary>نتيجة معالجة عنصر مع حالة إيقاف AI المشتركة للدورة دون تخزين حالة في خدمة singleton.</summary>
public sealed record SourceItemProcessingResultDto(DateTimeOffset? AiPausedUntil, bool StopAi);
