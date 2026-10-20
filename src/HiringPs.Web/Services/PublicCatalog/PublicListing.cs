// قواعد مشتركة للصفحات العامة: حجم الصفحة، الفرص المنشورة الحالية، روابط التفاصيل والـslug.
using System.Globalization;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون PublicListing ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class PublicListing
{
    public const int PageSize = 12;

    /// <summary>ينفذ PageCount وفق قواعد PublicListing الحالية دون تغيير عقد المستدعين.</summary>
    public static int PageCount(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);

    /// <summary>Published opportunities that have not passed their deadline (Palestine date).</summary>
    public static IQueryable<Opportunity> Current(this IQueryable<Opportunity> query, DateOnly today) =>
        query.Where(x =>
            x.Status == PublicationStatus.Published && (x.Deadline == null || x.Deadline >= today)
        );

    /// <summary>Current opportunities whose details are public; the single predicate for sitemaps and feeds.</summary>
    public static IQueryable<Opportunity> PublicDetails(
        this IQueryable<Opportunity> query,
        HiringDb db,
        DateOnly today
    ) =>
        query
            .Current(today)
            .Where(x => db.OpportunityAccessRules.Any(rule => rule.Kind == x.Kind && rule.PublicDetails));

    /// <summary>Content-change date for sitemaps/feeds; never the verification date.</summary>
    public static DateTimeOffset ContentDate(Opportunity item) =>
        item.SourceModifiedAt ?? item.SourcePublishedAt ?? item.CreatedAt;

    /// <summary>ينفذ DetailsPath وفق قواعد PublicListing الحالية دون تغيير عقد المستدعين.</summary>
    public static string DetailsPath(long id, string title)
    {
        var slug = UrlSlug.Create(title);
        return "/Opportunities/Details/"
            + id.ToString(CultureInfo.InvariantCulture)
            + (slug.Length > 0 ? "/" + Uri.EscapeDataString(slug) : "");
    }

    /// <summary>ينفذ DetailsPath وفق قواعد PublicListing الحالية دون تغيير عقد المستدعين.</summary>
    public static string DetailsPath(Opportunity item) => DetailsPath(item.Id, item.Title);

    /// <summary>Days until the deadline when it is within a week; null otherwise.</summary>
    public static int? ClosingInDays(Opportunity item, DateOnly today) =>
        item.Deadline is { } deadline && deadline >= today && deadline.DayNumber - today.DayNumber <= 7
            ? deadline.DayNumber - today.DayNumber
            : null;

    /// <summary>Published (or first seen) within the last 24 hours, measured on Palestine time.</summary>
    public static bool IsNew(Opportunity item)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SourceSchedule.Zone);
        var published = TimeZoneInfo.ConvertTime(
            item.SourcePublishedAt ?? item.CreatedAt,
            SourceSchedule.Zone
        );
        var age = now - published;
        return age >= TimeSpan.FromHours(-1) && age <= TimeSpan.FromHours(24);
    }
}
