using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>يستخرج صفحة الوظيفة الأصلية، ولا يعامل مقدمة RSS المبتورة كمحتوى كامل.</summary>
public static class RssJobPage
{
    /// <summary>يكشف نهاية الموجز المبتورة أو فقدان نص الإعلان قبل روابط المصدر.</summary>
    public static bool IsSummary(string text)
    {
        var body = SourceFieldQuotes.Main(text).Trim();
        return body.Length < 80 || body.EndsWith("...", StringComparison.Ordinal) || body.EndsWith('…');
    }

    /// <summary>يختار حاوية تفاصيل الإعلان، مستبعدًا الوظائف المقترحة وعناصر التنقل.</summary>
    public static string ContentHtml(string html)
    {
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var node in doc.QuerySelectorAll("script,style,nav,footer,noscript,iframe,aside,.related-jobs,#related-jobs,.similar-jobs,.recommended-jobs")) node.Remove();
        foreach (var header in doc.QuerySelectorAll("header"))
            if (header.Closest("article,main,[role='main'],#job-details,.job-details,#job-description,.job-description,.job-descriptions,#jobDescription,[itemprop='description']") is null) header.Remove();
        var details = doc.QuerySelectorAll("#job-details,.job-details,#job-description,.job-description,.job-descriptions,#jobDescription,[itemprop='description']")
            .Where(x => x.TextContent.Trim().Length >= 80).OrderByDescending(x => x.TextContent.Length).FirstOrDefault();
        var fallback = doc.QuerySelectorAll("article,main,[role='main']")
            .Where(x => x.TextContent.Trim().Length >= 80
                && !x.QuerySelectorAll("article,main,[role='main']").Any(child => child.TextContent.Trim().Length >= 80))
            .OrderByDescending(x => x.TextContent.Length - x.QuerySelectorAll("a").Sum(a => a.TextContent.Length) * 3).FirstOrDefault();
        var result = (details ?? fallback)?.InnerHtml
            ?? throw new ContentNeedsReviewException("Original job detail page is incomplete; RSS summary is not sufficient for extraction.");
        var text = new HtmlParser().ParseDocument(result).Body?.TextContent ?? "";
        if (IsSummary(text) || text.Contains("Verify you are human", StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(text, @"responsibilities|qualifications|requirements|job\s+description|workplace|work\s+location|how\s+to\s+apply|deadline|المهام|المؤهلات|الشروط|تفاصيل\s+الوظيفة|مكان\s+العمل|موقع\s+العمل|موعد\s+للتقديم", RegexOptions.IgnoreCase))
            throw new ContentNeedsReviewException("Original job detail page is incomplete; RSS summary is not sufficient for extraction.");
        return result;
    }
}
