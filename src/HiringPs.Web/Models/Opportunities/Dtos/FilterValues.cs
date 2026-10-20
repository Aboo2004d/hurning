// قوائم الفلاتر وصفحات التصفح (المدن والجهات) مخزنة مؤقتًا في الذاكرة لعشر دقائق.
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HiringPs.Web.Models;

/// <summary>مكون FilterValues ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record FilterValues(
    List<string> Categories,
    List<string> EmploymentTypes,
    List<string> ExperienceLevels,
    List<string> Locations
);
