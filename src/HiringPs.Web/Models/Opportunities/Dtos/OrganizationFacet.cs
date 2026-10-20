// قوائم الفلاتر وصفحات التصفح (المدن والجهات) مخزنة مؤقتًا في الذاكرة لعشر دقائق.
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HiringPs.Web.Models;

/// <summary>مكون OrganizationFacet ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record OrganizationFacet(
    string Slug,
    string Name,
    string[] Keys,
    int Count,
    DateTimeOffset LastModified
);
