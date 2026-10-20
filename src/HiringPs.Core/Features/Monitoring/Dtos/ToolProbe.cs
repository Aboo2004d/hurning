using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>DTO لنتيجة أداة مع حالة ورمز ترجمة وتفاصيل تشخيصية آمنة.</summary>
public record ToolProbe(string Tool, ToolState State, string Code, string Detail = "");
