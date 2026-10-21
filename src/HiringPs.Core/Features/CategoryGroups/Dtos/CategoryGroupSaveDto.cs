namespace HiringPs.Core;

/// <summary>أمر اعتماد المجموعة؛ الأسماء المختارة يجب أن تكون من الكتالوج أو المجموعة نفسها.</summary>
public sealed record CategoryGroupSaveDto(int Id, string Name, string[] Members, byte[] Version, string Actor);
