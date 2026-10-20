using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>سياق استدعاء AI يربطه بنسخة تعليمات محددة وعنصر مصدر اختياري.</summary>
public record AiExecution(PromptVersion Version, long? SourceItemId);
