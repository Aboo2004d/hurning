// قواعد مشتركة للصفحات العامة: حجم الصفحة، الفرص المنشورة الحالية، روابط التفاصيل والـslug.
using System.Globalization;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public static class SiteCache
{
    /// <summary>Output-cache policy for robots, sitemaps, llms.txt and feeds.</summary>
    public const string Files = "site-files";

    /// <summary>Short output-cache policy for anonymous listing pages.</summary>
    public const string Listing = "site-listing";
}

public static class PublicListing
{
    public const int PageSize = 12;

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

    public static string DetailsPath(long id, string title)
    {
        var slug = UrlSlug.Create(title);
        return "/Opportunities/Details/"
            + id.ToString(CultureInfo.InvariantCulture)
            + (slug.Length > 0 ? "/" + Uri.EscapeDataString(slug) : "");
    }

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

public static class KindSlugs
{
    public static string For(OpportunityKind kind) =>
        kind switch
        {
            OpportunityKind.Job => "jobs",
            OpportunityKind.Training => "training",
            OpportunityKind.Tender => "tenders",
            _ => "procurement",
        };

    public static OpportunityKind? Parse(string? slug) =>
        slug?.ToLowerInvariant() switch
        {
            "jobs" => OpportunityKind.Job,
            "training" => OpportunityKind.Training,
            "tenders" => OpportunityKind.Tender,
            "procurement" => OpportunityKind.ProcurementNotice,
            _ => null,
        };

    public const string RouteConstraint = "regex(^(jobs|training|tenders|procurement)$)";
}

/// <summary>Readable URL segments; keeps Arabic and Latin letters/digits and joins words with '-'.</summary>
public static class UrlSlug
{
    public static string Create(string? text, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var value = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(value.Length);
        var dash = false;
        foreach (var c in value)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format || c == 'ـ')
                continue;
            if (char.IsLetterOrDigit(c))
            {
                if (dash && builder.Length > 0)
                    builder.Append('-');
                builder.Append(c);
                dash = false;
            }
            else
                dash = true;
        }
        var slug = builder.ToString();
        if (slug.Length <= maxLength)
            return slug;
        var cut = slug.LastIndexOf('-', maxLength);
        return (cut > maxLength / 2 ? slug[..cut] : slug[..maxLength]).Trim('-');
    }
}
