using System.Text.Json;
using System.Text.RegularExpressions;
namespace HiringPs.Core;

/// <summary>يستكمل الحقول الفارغة من تسميات صريحة فقط ويحافظ على رد النموذج الخام للتشخيص.</summary>
public static class ExplicitExtractionRecovery
{
    /// <summary>يستعيد ما يثبته نص دور واحد أو دليل المكان الخاص به؛ لا ينقل بيانات مشتركة بين أدوار متعددة.</summary>
    public static CompletedExtractionDto Complete(ExtractedOpportunity raw, Opportunity? previous,
        string source, bool singleOpportunity, IEnumerable<string> approvedLocations, IEnumerable<string>? approvedCategories = null)
    {
        var value = JsonSerializer.Deserialize<ExtractedOpportunity>(JsonSerializer.Serialize(raw, AiExtractor.JsonOptions), AiExtractor.JsonOptions)!;
        var changed = new List<string>();
        var warnings = new List<string>();
        var context = singleOpportunity ? SourceFieldQuotes.Main(source) : "";
        var quotes = SourceFieldQuotes.Locations(context);
        if (TextEvidence.Contains(SourceFieldQuotes.Main(source), raw.LocationEvidence))
        {
            // For multi-role announcements use only that role's own explicit location evidence.
            quotes.AddRange(SourceFieldQuotes.Locations(raw.LocationEvidence));
        }
        // A ministry/department allocation is not a geographic address and must not
        // contradict a separate explicit city field from the same announcement.
        quotes = quotes.Where(x => !Regex.IsMatch(x, @"وزارة|ديوان|دائرة|\bdepartment\b|\bministry\b", RegexOptions.IgnoreCase)
            || ExplicitWorkLocationRules.KnownLocal(x, approvedLocations)
            || ExplicitWorkLocationRules.HasForeignLocation(x)).ToList();
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
                var type = MostSpecificConsistent(SourceFieldQuotes.EmploymentTypes(context));
                if (type is not null) { value.EmploymentType = type; changed.Add(nameof(value.EmploymentType)); }
            }
            // Previous source-supported values remain usable; unsupported values are not silently carried forward.
            if (string.IsNullOrWhiteSpace(value.EmploymentType) && !string.IsNullOrWhiteSpace(previous?.EmploymentType)
                && TextEvidence.Contains(context, previous.EmploymentType))
            { value.EmploymentType = previous.EmploymentType; changed.Add(nameof(value.EmploymentType)); }

        }
        if ((quotes.Count > 1 && location is null)
            || (location is not null && !ExplicitWorkLocationRules.ConsistentScope(location, value.Locations)))
            warnings.Add("Conflicting explicit work locations; review the original announcement.");
        if (!AiExtractionContract.ValidWorkMode(value.WorkMode))
        {
            value.WorkMode = "";
            changed.Add(nameof(value.WorkMode));
            warnings.Add("AI extraction quality: workMode must contain only On-site, Remote, Hybrid or an empty value.");
        }
        var classification = CategoryTaxonomy.Classify(value, context, approvedCategories);
        if (value.Category != classification.Category)
        {
            value.Category = classification.Category;
            changed.Add(nameof(value.Category));
        }
        if (classification.ReviewReason.Length > 0) warnings.Add(classification.ReviewReason);
        return new(value, changed.Distinct().ToArray(), warnings.ToArray());
    }

    /// <summary>يختار العنوان الأكثر تفصيلًا إذا كانت جميع القيم الأخرى أجزاء منه؛ الخلاف يبقى للمراجعة.</summary>
    private static string? MostSpecificConsistent(IEnumerable<string> values)
    {
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length).ToList();
        return list.Count == 0 || !list.All(x => ExplicitWorkLocationRules.ConsistentScope(list[0], x)) ? null : list[0];
    }
}
