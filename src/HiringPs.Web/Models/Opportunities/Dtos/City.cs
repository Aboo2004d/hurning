// أسماء المدن بالعربية والإنجليزية وروابطها الثابتة، مع ربطها بأسماء البحث في LocationSearch.
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون City ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record City(string Slug, string Arabic, string English)
{
    /// <summary>ينفذ Name وفق قواعد City الحالية دون تغيير عقد المستدعين.</summary>
    public string Name(bool english) => english ? English : Arabic;

    /// <summary>Value matched against Opportunity.LocationSearchText (which already contains aliases).</summary>
    public string SearchKey => LocationSearch.Normalize(Arabic);
}
