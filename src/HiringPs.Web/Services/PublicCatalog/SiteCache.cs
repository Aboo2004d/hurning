// قواعد مشتركة للصفحات العامة: حجم الصفحة، الفرص المنشورة الحالية، روابط التفاصيل والـslug.
using System.Globalization;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون SiteCache ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class SiteCache
{
    /// <summary>Output-cache policy for robots, sitemaps, llms.txt and feeds.</summary>
    public const string Files = "site-files";

    /// <summary>Short output-cache policy for anonymous listing pages.</summary>
    public const string Listing = "site-listing";
}
