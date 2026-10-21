namespace HiringPs.Core;

/// <summary>اسم مجال وانتماؤه الحالي لعرض الاختيارات المسموحة وتفسير غير المتاح.</summary>
public sealed record CategoryGroupOptionDto(string Label, int? GroupId, string? GroupName);
