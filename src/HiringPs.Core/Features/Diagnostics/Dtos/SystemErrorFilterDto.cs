using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>مرشحات الأخطاء؛ حدود الأيام بتوقيت فلسطين، والتحقق على معاملات record ليوافق ربط MVC.</summary>
public sealed record SystemErrorFilterDto(
    [MaxLength(200)] string? Search = null,
    [MaxLength(32)] string? Application = null,
    [MaxLength(16)] string? Level = null,
    DateOnly? From = null, DateOnly? To = null);
