using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>قيم صريحة مقتبسة من الإعلان الأساسي، مع حدود حقول الصفحات المسطحة وMarkdown.</summary>
public static class SourceFieldQuotes
{
    private const string Work = @"موقع\s+(?:العمل|الوظيفة)|مكان\s+(?:العمل|الوظيفة)|duty\s+station|work\s+location|job\s+location|workplace";
    private const string Types = @"طبيعة\s+الدوام|نوع\s+(?:الدوام|الوظيفة)|employment\s+type|job\s+type";
    private const string Other = @"المسمى\s+الوظيفي|طبيعة\s+العمل|الراتب|الجنس|تاريخ\s+النشر|اخر\s+تاريخ\s+للتقديم|آخر\s+موعد|الشركة|عدد\s+الشواغر|سنوات\s+الخبرة|الايميل|الهاتف|مشاركة|المهام|الخبرة|المهارات|متطلبات\s+الوظيفة|المؤهل\s+العلمي|المؤهلات\s+العلمية|الفئة\s+المستهدفة|الاختصاص\s+العام\s+للوظيفة|طريقة\s+التقديم|آلية\s+التقديم|eligibility|education(?:\s+requirements)?|application\s+deadline|salary|gender|company|experience|responsibilities|requirements|posting\s+period|job\s+opening\s+(?:number|id)|staffing\s+exercise\s+id|department(?:\s*/\s*office)?";

    public static List<string> Locations(string source) => Read(Main(source), Work);
    public static List<string> EmploymentTypes(string source) => Read(Main(source), Types);

    /// <summary>يعزل الإعلان دون الوظائف المقترحة وروابط التنقل.</summary>
    public static string Main(string source)
    {
        foreach (var marker in new[] { "SOURCE LINKS:", "وظائف مشابهة", "وظائف ذات صلة", "Related jobs", "Recommended jobs", "Suggested jobs" })
        {
            var i = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) source = source[..i];
        }
        return source;
    }

    /// <summary>يحافظ على الاقتباس الأصلي ويتوقف عند التسمية التالية، حتى عند غياب الأسطر الجديدة.</summary>
    private static List<string> Read(string source, string labels)
    {
        var result = new List<string>();
        foreach (Match match in Regex.Matches(source, @"(?<![\p{L}\p{N}])(?:" + labels + @")\s*\*{0,2}\s*[:：]\s*\*{0,2}\s*", RegexOptions.IgnoreCase))
        {
            var tail = source[(match.Index + match.Length)..];
            var boundary = Regex.Match(tail, @"[\r\n*#]|---|(?<![\p{L}\p{N}])(?:" + Work + "|" + Types + "|" + Other + @")\s*[:：]|SOURCE LINKS:", RegexOptions.IgnoreCase);
            var text = (boundary.Success ? tail[..boundary.Index] : tail).Trim();
            if (text.Length is > 0 and <= 400 && !text.Contains("https://", StringComparison.OrdinalIgnoreCase)
                && text is not ("..." or "…" or "-")) result.Add(text);
        }
        return result;
    }
}
