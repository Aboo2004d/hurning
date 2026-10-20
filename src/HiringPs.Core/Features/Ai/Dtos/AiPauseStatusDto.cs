namespace HiringPs.Core;

/// <summary>حالة الإيقاف العام المحفوظة للعرض دون تشغيل AI أو تعديل حالة الحاكم.</summary>
public sealed record AiPauseStatusDto(DateTimeOffset? Until, string Reason)
{
    /// <summary>يميز المهلة السارية عن سجل إيقاف قديم انتهى؛ لا يثبت جاهزية العامل أو المزود.</summary>
    public bool IsPaused(DateTimeOffset now) => Until is { } until && until > now;
}
