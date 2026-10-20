namespace HiringPs.Core;

/// <summary>صف الاسم البديل والاسم المعتمد وسند إنشائه للعرض الإداري.</summary>
public sealed record OrganizationAliasSummaryDto(int Id, string Alias, string Canonical, string CreatedBy, DateTimeOffset CreatedAt);
