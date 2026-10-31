using System.Text.Json;
using System.Text.RegularExpressions;
namespace HiringPs.Core;

/// <summary>يستكمل الحقول الفارغة من تسميات صريحة فقط ويحافظ على رد النموذج الخام للتشخيص.</summary>
public static class ExplicitExtractionRecovery
{
    private const string WorkLabels = @"موقع\s+(?:العمل|الوظيفة)|مكان\s+(?:العمل|الوظيفة)|duty\s+station|work\s+location|job\s+location";
    private const string TypeLabels = @"طبيعة\s+الدوام|نوع\s+(?:الدوام|الوظيفة)|employment\s+type|job\s+type";
    private const string DegreeLabels = @"المؤهل\s+العلمي|المؤهلات\s+العلمية|الفئة\s+المستهدفة|eligibility|education(?:\s+requirements)?";
    private const string NextLabels = @"المسمى\s+الوظيفي|طبيعة\s+العمل|الراتب|الجنس|تاريخ\s+النشر|اخر\s+تاريخ\s+للتقديم|آخر\s+موعد|الشركة|عدد\s+الشواغر|سنوات\s+الخبرة|الايميل|الهاتف|مشاركة|المهام|الخبرة|المهارات|متطلبات\s+الوظيفة|application\s+deadline|salary|gender|company|experience|responsibilities|requirements";

    /// <summary>يستعيد ما يثبته نص دور واحد أو دليل المكان الخاص به؛ لا ينقل بيانات مشتركة بين أدوار متعددة.</summary>
    public static CompletedExtractionDto Complete(ExtractedOpportunity raw, Opportunity? previous,
        string source, bool singleOpportunity, IEnumerable<string> approvedLocations, IEnumerable<string>? approvedCategories = null)
    {
        var value = JsonSerializer.Deserialize<ExtractedOpportunity>(JsonSerializer.Serialize(raw, AiExtractor.JsonOptions), AiExtractor.JsonOptions)!;
        var changed = new List<string>();
        var warnings = new List<string>();
        var context = singleOpportunity ? MainAnnouncement(source) : "";
        var quotes = Fields(context, WorkLabels);
        if (TextEvidence.Contains(MainAnnouncement(source), raw.LocationEvidence))
        {
            // For multi-role announcements use only that role's own explicit location evidence.
            quotes.AddRange(Fields(raw.LocationEvidence, WorkLabels));
        }
        var location = MostSpecificConsistent(quotes);
        if (string.IsNullOrWhiteSpace(value.Locations) && location is not null)
        {
            value.Locations = location;
            value.LocationEvidence = location;
            changed.Add(nameof(value.Locations)); changed.Add(nameof(value.LocationEvidence));
            if (value.IsInPalestine is null && ExplicitWorkLocationRules.KnownLocal(location, approvedLocations))
            { value.IsInPalestine = true; changed.Add(nameof(value.IsInPalestine)); }
        }
        else if (!string.IsNullOrWhiteSpace(value.Locations) && location is not null
            && TextEvidence.Contains(location, value.Locations) && !TextEvidence.Contains(source, value.LocationEvidence))
        {
            value.LocationEvidence = location; changed.Add(nameof(value.LocationEvidence));
        }
        // Preserve the last displayed value if a new extraction provides none, but keep it in review.
        // Mere mention of an old city elsewhere in the text cannot re-approve a work location.
        if (string.IsNullOrWhiteSpace(value.Locations) && !string.IsNullOrWhiteSpace(previous?.Locations))
        {
            value.Locations = previous.Locations;
            value.LocationEvidence = "";
            warnings.Add("Stored location retained for review; current extraction did not establish the work location.");
            changed.Add(nameof(value.Locations));
        }
        if (value.Kind == OpportunityKind.Job && context.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(value.EmploymentType))
            {
                var type = MostSpecificConsistent(Fields(context, TypeLabels));
                if (type is not null) { value.EmploymentType = type; changed.Add(nameof(value.EmploymentType)); }
            }
            // Previous source-supported values remain usable; unsupported values are not silently carried forward.
            if (string.IsNullOrWhiteSpace(value.EmploymentType) && !string.IsNullOrWhiteSpace(previous?.EmploymentType)
                && TextEvidence.Contains(context, previous.EmploymentType))
            { value.EmploymentType = previous.EmploymentType; changed.Add(nameof(value.EmploymentType)); }

        }
        if ((quotes.Count > 1 && location is null)
            || (location is not null && !ExplicitWorkLocationRules.Compatible(location, value.Locations)))
            warnings.Add("Conflicting explicit work locations; review the original announcement.");
        var classification = CategoryTaxonomy.Classify(value, context, approvedCategories);
        if (value.Category != classification.Category)
        {
            value.Category = classification.Category;
            changed.Add(nameof(value.Category));
        }
        if (classification.ReviewReason.Length > 0) warnings.Add(classification.ReviewReason);
        return new(value, changed.Distinct().ToArray(), warnings.ToArray());
    }

    /// <summary>يقصر الاستكمال على الإعلان الرئيسي دون روابط المصدر والوظائف المقترحة.</summary>
    private static string MainAnnouncement(string source)
    {
        foreach (var marker in new[] { "SOURCE LINKS:", "وظائف مشابهة", "وظائف ذات صلة", "Related jobs", "Recommended jobs", "Suggested jobs" })
        {
            var index = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) source = source[..index];
        }
        return source;
    }

    /// <summary>يقتبس قيمة الحقل من النص الأصلي، ويتوقف عند التسمية التالية أو حد تنسيق Markdown.</summary>
    private static List<string> Fields(string source, string labels)
    {
        var result = new List<string>();
        var matches = Regex.Matches(source, @"(?<![\p{L}\p{N}])(?:" + labels + @")\s*\*{0,2}\s*[:：]\s*\*{0,2}\s*", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var tail = source[(match.Index + match.Length)..];
            var boundary = Regex.Match(tail, @"[\r\n*#]|---|(?<![\p{L}\p{N}])(?:" + WorkLabels + "|" + TypeLabels + "|" + DegreeLabels + "|" + NextLabels + @")\s*[:：]|SOURCE LINKS:", RegexOptions.IgnoreCase);
            var text = (boundary.Success ? tail[..boundary.Index] : tail).Trim();
            if (text.Length is > 0 and <= 400 && !text.Contains("https://", StringComparison.OrdinalIgnoreCase)) result.Add(text);
        }
        return result;
    }

    /// <summary>يختار العنوان الأكثر تفصيلًا إذا كانت جميع القيم الأخرى أجزاء منه؛ الخلاف يبقى للمراجعة.</summary>
    private static string? MostSpecificConsistent(IEnumerable<string> values)
    {
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length).ToList();
        return list.Count == 0 || !list.All(x => ExplicitWorkLocationRules.Compatible(list[0], x)) ? null : list[0];
    }
}
