using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>مرشحات الأخطاء؛ حدود الأيام بتوقيت فلسطين.</summary>
public sealed record SystemErrorFilterDto(
    [property: MaxLength(200)] string? Search = null,
    [property: MaxLength(32)] string? Application = null,
    [property: MaxLength(16)] string? Level = null,
    DateOnly? From = null, DateOnly? To = null);
