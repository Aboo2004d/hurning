using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>Conservative discovery from the rendered DOM; never crawls arbitrary navigation links.</summary>
public static class AutomaticSource
{
    private static readonly Regex OpportunityWords = new(
        @"وظيف|وظائف|شاغر|توظيف|مطلوب|عطاء|مناقص|تدريب|\b(job|vacanc\w*|career|tender|procurement|training|internship)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    private static readonly Regex NavigationWords = new(
        @"^(وظائف|وظائف شاغرة|عطاءات|مناقصات|تدريب|jobs|careers|vacancies|tenders|training|login|register|home|about|contact|الرئيسية|تسجيل الدخول)$",
        RegexOptions.IgnoreCase
    );

    /// <summary>يستخرج روابط الإعلانات التي يمكن إثباتها من الصفحة الحالية.</summary>
    public static (string[] Links, string? Next) Discover(string html, Uri listing)
    {
        var doc = new HtmlParser().ParseDocument(html);
        var anchors = doc.QuerySelectorAll("a[href]")
            .Where(a => a.Closest("nav,header,footer") is null)
            .ToArray();
        var links = new HashSet<string>();
        foreach (var a in anchors)
        {
            var label = Regex.Replace(a.TextContent, @"\s+", " ").Trim();
            if (label.Length < 5 || NavigationWords.IsMatch(label))
                continue;
            if (
                !Uri.TryCreate(listing, a.GetAttribute("href"), out var uri)
                || uri.Scheme != "https"
                || uri.IdnHost != listing.IdnHost
                || uri.GetLeftPart(UriPartial.Path) == listing.GetLeftPart(UriPartial.Path)
                    && uri.Query == listing.Query
            )
                continue;
            if (a.GetAttribute("href")!.StartsWith('#'))
                continue;
            var card = a.Closest("article,li,tr,[itemtype*='JobPosting']");
            var evidence = label + " " + uri.AbsolutePath + " " + uri.Query;
            var specificPath =
                uri.AbsolutePath.Trim('/').Split('/').Length >= 2
                || uri.Query.Length > 0
                || uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (
                specificPath && OpportunityWords.IsMatch(evidence)
                || card is not null
                    && card.TextContent.Length is > 60 and < 4000
                    && OpportunityWords.IsMatch(card.TextContent)
            )
                links.Add(new UriBuilder(uri) { Fragment = "" }.Uri.ToString());
        }

        string? next = null;
        foreach (var a in doc.QuerySelectorAll("a[href]"))
        {
            var text = a.TextContent.Trim();
            if (
                !(
                    a.GetAttribute("rel")?.Split(' ').Contains("next") == true
                    || Regex.IsMatch(
                        text,
                        @"^(التالي|التالية|الصفحة التالية|next|next page|›|»)$",
                        RegexOptions.IgnoreCase
                    )
                )
            )
                continue;
            if (a.GetAttribute("aria-disabled") == "true" || a.ClassList.Contains("disabled"))
                continue;
            if (
                Uri.TryCreate(listing, a.GetAttribute("href"), out var uri)
                && uri.Scheme == "https"
                && uri.IdnHost == listing.IdnHost
                && uri != listing
            )
            {
                next = uri.ToString();
                break;
            }
        }

        if (links.Count > 250)
            throw new InvalidOperationException(
                "Automatic listing limit reached; source needs review."
            );
        if (
            next is null
            && doc.QuerySelectorAll("button,[role='button']")
                .Any(e =>
                    !e.HasAttribute("disabled")
                    && e.GetAttribute("aria-disabled") != "true"
                    && Regex.IsMatch(
                        e.TextContent.Trim(),
                        @"^(التالي|التالية|المزيد|عرض المزيد|تحميل المزيد|next|next page|load more|show more)$",
                        RegexOptions.IgnoreCase
                    )
                )
        )
            throw new InvalidOperationException(
                "Interactive pagination detected; source needs a tested adapter before full collection."
            );
        return (links.ToArray(), next);
    }

    /// <summary>يختار جزء المحتوى المناسب من HTML وفق قواعد الاكتشاف التلقائي.</summary>
    public static string ContentHtml(string html)
    {
        var doc = new HtmlParser().ParseDocument(html);
        foreach (
            var e in doc.QuerySelectorAll(
                "script,style,nav,header,footer,noscript,iframe,aside,form"
            )
        )
            e.Remove();
        var candidates = doc.QuerySelectorAll("[itemprop='description'],article,main,[role='main']")
            .Where(e => e.TextContent.Trim().Length >= 80)
            .ToArray();
        var best = candidates
            .Where(e =>
                !e.QuerySelectorAll("[itemprop='description'],article,main,[role='main']")
                    .Any(child => child.TextContent.Trim().Length >= 80)
            )
            .OrderByDescending(e =>
                e.TextContent.Length - e.QuerySelectorAll("a").Sum(a => a.TextContent.Length) * 3
            )
            .FirstOrDefault();
        if (best is null)
            throw new ContentNeedsReviewException(
                "Automatic content discovery is uncertain; source needs review."
            );
        return best.InnerHtml;
    }
}
