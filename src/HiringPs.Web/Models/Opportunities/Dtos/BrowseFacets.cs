// قوائم الفلاتر وصفحات التصفح (المدن والجهات) مخزنة مؤقتًا في الذاكرة لعشر دقائق.
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HiringPs.Web.Models;

/// <summary>مكون BrowseFacets ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record BrowseFacets(List<CityFacet> Cities, List<OrganizationFacet> Organizations)
{
    /// <summary>ينفذ Organization وفق قواعد BrowseFacets الحالية دون تغيير عقد المستدعين.</summary>
    public OrganizationFacet? Organization(string? slug) =>
        Organizations.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>ينفذ OrganizationFor وفق قواعد BrowseFacets الحالية دون تغيير عقد المستدعين.</summary>
    public OrganizationFacet? OrganizationFor(string normalizedOrganization) =>
        Organizations.FirstOrDefault(x => x.Keys.Contains(normalizedOrganization));
}
