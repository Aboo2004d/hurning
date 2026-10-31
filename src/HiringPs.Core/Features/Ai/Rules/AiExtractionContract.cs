using System.Text.Json;
using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>يتحقق من مفاتيح الرد الأصلية ومن اكتمال الحقول دون ملء الحقائق بالتخمين.</summary>
public static class AiExtractionContract
{
    public static readonly string[] Fields = typeof(ExtractedOpportunity).GetProperties()
        .Select(x => JsonNamingPolicy.CamelCase.ConvertName(x.Name)).ToArray();
    public static readonly string[] WorkModes = ["", "On-site", "Remote", "Hybrid"];

    /// <summary>يفحص JSON قبل أن تخفي القيم الافتراضية في DTO المفاتيح التي أغفلها النموذج.</summary>
    public static void ValidateJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("inScope", out var scope)
            || scope.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("opportunities", out var children) || children.ValueKind != JsonValueKind.Array)
            throw new AiExtractionRejectedException("AI extraction contract: expected inScope and opportunities.");
        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object)
                throw new AiExtractionRejectedException("AI extraction contract: opportunity must be an object.");
            var missing = Fields.Where(name => !child.TryGetProperty(name, out _)).ToArray();
            if (missing.Length > 0)
                throw new AiExtractionRejectedException("AI extraction contract: omitted fields: " + string.Join(", ", missing) + ".");
            if (child.EnumerateObject().GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new AiExtractionRejectedException("AI extraction contract: duplicate field names.");
            foreach (var field in typeof(ExtractedOpportunity).GetProperties().Where(x => x.PropertyType == typeof(string)))
                if (child.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(field.Name)).ValueKind != JsonValueKind.String)
                    throw new AiExtractionRejectedException("AI extraction contract: unknown text must be an empty string, not null.");
            if (child.GetProperty("otherDates").ValueKind != JsonValueKind.Array
                || child.GetProperty("deadlineIsExtension").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || child.GetProperty("isInPalestine").ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
                || child.GetProperty("kind").ValueKind != JsonValueKind.String
                || !Enum.GetNames<OpportunityKind>().Contains(child.GetProperty("kind").GetString(), StringComparer.OrdinalIgnoreCase)
                || child.GetProperty("originalLanguage").GetString() is not ("ar" or "en"))
                throw new AiExtractionRejectedException("AI extraction contract: invalid array, boolean, language or kind.");

        }
    }

    /// <summary>يرفض خلط نمط العمل بالتفاصيل والحقول الصريحة المتروكة فارغة؛ النص المتعدد الأدوار لا يُنسخ بينها.</summary>
    public static List<string> Issues(ExtractionResult result, string source)
    {
        var issues = new List<string>();
        if (!result.InScope) return issues;
        foreach (var role in result.Opportunities)
        {
            if (string.IsNullOrWhiteSpace(role.Category) && CategoryTaxonomy.Classify(role).Category.Length > 0)
                issues.Add("AI extraction quality: a source-supported professional category was omitted.");
            if (!ValidWorkMode(role.WorkMode)) issues.Add("AI extraction quality: workMode must contain only On-site, Remote, Hybrid or an empty value.");
            if (role.Kind != OpportunityKind.Job && (role.WorkMode.Length > 0 || role.EmploymentType.Length > 0 || role.ExperienceLevel.Length > 0))
                issues.Add("AI extraction quality: job-only fields were filled for a non-job opportunity.");
        }
        var duplicateRoles = result.Opportunities.GroupBy(x => new {
            x.Kind, Title = CategorySimilarityRules.Normalize(x.Title), Organization = TextRules.OrganizationKey(x.Organization),
            Location = LocationSearch.Normalize(x.Locations), Reference = DuplicateMatcher.ReferenceKey(x.ReferenceNumber),
            x.EmploymentType, x.ExperienceLevel, x.Duration, x.Eligibility, x.Deadline, x.WorkMode
        }).Any(x => x.Count() > 1);
        if (duplicateRoles) issues.Add("AI extraction quality: repeated role identities without distinct source-supported allocations.");
        if (result.Opportunities.Count == 1)
        {
            var role = result.Opportunities[0];
            if ((role.Locations.Length == 0 || role.LocationEvidence.Length == 0)
                && SourceFieldQuotes.Locations(source).Any(x => ExplicitWorkLocationRules.KnownLocal(x, [])))
                issues.Add("AI extraction quality: explicitly stated work location or its evidence was omitted.");
            if (role.Kind == OpportunityKind.Job && role.EmploymentType.Length == 0 && SourceFieldQuotes.EmploymentTypes(source).Count > 0)
                issues.Add("AI extraction quality: explicitly stated employment type was omitted.");
            // This detects only unmistakable dated closing clauses. It is not the deadline
            // acceptance policy: unfamiliar closing expressions remain supported by the date rules.
            if (role.Deadline is null && role.DeadlineEvidence.Length == 0
                && Regex.IsMatch(SourceFieldQuotes.Main(source), @"(?:اخر\s+موعد\s+للتقديم|آخر\s+موعد\s+للتقديم|اخر\s+موعد\s+لتقديم\s+العروض|آخر\s+موعد\s+لتقديم\s+العروض|application\s+deadline|closing\s+date|apply\s+by)[^\r\n]{0,90}?(?:\d{4}\s*[-/]\s*\d{1,2}\s*[-/]\s*\d{1,2}|\d{1,2}\s*[-/]\s*\d{1,2}\s*[-/]\s*\d{4})", RegexOptions.IgnoreCase))
                issues.Add("AI extraction quality: an explicit dated submission deadline was omitted.");
        }
        return issues.Distinct().ToList();
    }

    /// <summary>يتحقق من نمط مختصر فقط؛ لا يختصر نصًا خاطئًا ليبدو صالحًا.</summary>
    public static bool ValidWorkMode(string? value) => value is not null && WorkModes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>يرفض إعادة استخدام رد محفوظ ناقص أو مشوه مع السماح باسترجاعه للتشخيص.</summary>
    public static bool CanReuse(string json, string source)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            ValidateJson(document.RootElement);
            var result = JsonSerializer.Deserialize<ExtractionResult>(json, AiExtractor.JsonOptions);
            if (result is null) return false;
            ExtractionValidator.EnsureShape(result);
            return Issues(result, source).Count == 0;
        }
        catch (Exception ex) when (ex is JsonException or ContentNeedsReviewException) { return false; }
    }
}
