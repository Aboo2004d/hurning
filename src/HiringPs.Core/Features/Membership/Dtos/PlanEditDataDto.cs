namespace HiringPs.Core;

/// <summary>بيانات تعديل باقة مع رمز التزامن وشروط النسخة الحالية.</summary>
public sealed record PlanEditDataDto(int Id, bool Visible, int SortOrder, string Version,
    MembershipPlanVersionSummaryDto CurrentVersion);
