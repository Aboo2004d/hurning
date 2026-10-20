namespace HiringPs.Core;

/// <summary>صف باقة للإدارة مع شروط نسختها الحالية دون rowversion أو كيانات EF.</summary>
public sealed record AdminPlanSummaryDto(int Id, bool Visible, MembershipPlanVersionSummaryDto CurrentVersion);
