namespace HiringPs.Core;

/// <summary>مجموعة معتمدة للقراءة والتعديل دون كشف كيانات التخزين.</summary>
public sealed record CategoryGroupSummaryDto(int Id, string Name, DateTimeOffset UpdatedAt, string[] Members, byte[] Version);
