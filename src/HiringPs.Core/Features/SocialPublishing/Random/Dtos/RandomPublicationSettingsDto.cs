namespace HiringPs.Core;
/// <summary>إعدادات وجهة وحالة ربطها دون كشف رمز الوصول.</summary>
public sealed record RandomPublicationSettingsDto(string Provider, bool Enabled, int DailyCount, int MaxRemainingDays,
    string[] Times, string Version, bool Connected, string AccountName);
