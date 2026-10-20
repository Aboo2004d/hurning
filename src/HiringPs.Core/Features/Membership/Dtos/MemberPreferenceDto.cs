namespace HiringPs.Core;

/// <summary>خيارات تنبيه العضو بعد قراءة JSON دون كيان قابل للتعديل من الواجهة.</summary>
public sealed record MemberPreferenceDto(int Kinds, string[] Categories, string[] Locations);
