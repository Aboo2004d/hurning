namespace HiringPs.Core;

/// <summary>حالة اتصال عامل الجمع ونشاطه مع وقت آخر نبضة للعرض دون كشف إعدادات الاتصال.</summary>
public sealed record CollectionWorkerStatusDto(bool Connected, bool Collecting, DateTimeOffset? LastPulse);
