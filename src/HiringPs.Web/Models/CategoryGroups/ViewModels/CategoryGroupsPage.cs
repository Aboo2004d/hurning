using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>قائمتان مستقلتا الترقيم للمجموعات المعتمدة والمجالات الجديدة.</summary>
public sealed record CategoryGroupsPage(string? Search, AdminPage<CategoryGroupSummaryDto> Groups,
    UngroupedCategoryDto[] Ungrouped, AdminPager UngroupedPager);
