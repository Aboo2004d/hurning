namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـToolRequestStatus؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum ToolRequestStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}
