namespace HiringPs.Core;

/// <summary>نافذة المجالات غير المصنفة مع اقتراحات الصفحة فقط.</summary>
public sealed record UngroupedCategoryPageDto(int Page, int Total, UngroupedCategoryDto[] Items);
