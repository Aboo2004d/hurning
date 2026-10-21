namespace HiringPs.Core;

/// <summary>مجال مستقل مع اقتراحات انضمام يراجعها المسؤول.</summary>
public sealed record UngroupedCategoryDto(string Label, CategorySuggestionDto[] Suggestions);
