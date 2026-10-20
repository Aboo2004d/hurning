namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـToolState؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum ToolState
{
    Ready = 0,
    Problem = 1,
    NotConfigured = 2,
    Configured = 3,
    Disabled = 4,
    Unknown = 5,
}
