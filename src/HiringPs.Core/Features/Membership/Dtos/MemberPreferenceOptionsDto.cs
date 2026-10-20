namespace HiringPs.Core;

/// <summary>التصنيفات الحالية والمواقع المعتمدة قبل إضافة أسماء مدن كتالوج الواجهة.</summary>
public sealed record MemberPreferenceOptionsDto(string[] Categories, List<string> ApprovedLocations);
