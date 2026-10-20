namespace HiringPs.Core;

/// <summary>خيارات تحقق منها محرر التفضيلات؛ null للتذكيرات تعني إبقاء اختيارها السابق.</summary>
public sealed record MemberPreferenceUpdateDto(int Kinds, string[] Categories, string[] Locations, bool? SavedReminders);
