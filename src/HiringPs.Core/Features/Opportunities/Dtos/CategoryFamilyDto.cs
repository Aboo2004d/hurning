namespace HiringPs.Core;

/// <summary>مجال رئيسي وتخصصاته المعيارية؛ لا يمنح تكافؤ التنبيهات قبل اعتماد المجموعة.</summary>
public sealed record CategoryFamilyDto(string Key, string Name, string English, string[] Children);
