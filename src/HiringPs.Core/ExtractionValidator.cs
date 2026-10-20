// قواعد التحقق الحتمية من ناتج الاستخراج قبل حفظه: الشكل والحقول والأدلة والتواريخ.
namespace HiringPs.Core;

public static class ExtractionValidator
{
    // Normalises nulls and rejects structurally unusable output; such output needs a person, not a retry.
    public static void EnsureShape(ExtractionResult result)
    {
        if (result.Opportunities is null)
            throw new ContentNeedsReviewException("AI returned no opportunity array; review manually.");
        foreach (var value in result.Opportunities)
        {
            if (value is null)
                throw new ContentNeedsReviewException("AI returned an empty opportunity record; review manually.");
            foreach (
                var property in typeof(ExtractedOpportunity)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(string))
            )
                if (property.GetValue(value) is null)
                    property.SetValue(value, "");
            value.OtherDates ??= [];
            if (value.ChildKey.Length > 100)
                value.ChildKey = TextRules.Hash(value.ChildKey);
        }
        if (!result.InScope)
            return;
        if (result.Opportunities.Count is 0 or > 30)
            throw new ContentNeedsReviewException(
                "Invalid extracted opportunity count (0 or more than 30); review source manually."
            );
        if (
            result.Opportunities.Any(x => string.IsNullOrWhiteSpace(x.ChildKey))
            || result.Opportunities.Select(x => x.ChildKey).Distinct().Count()
                != result.Opportunities.Count
        )
            throw new ContentNeedsReviewException(
                "Missing/duplicate stable child keys; review source."
            );
    }

    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent)
    {
        var url = value?.Trim() ?? "";
        return
            url.Length is > 0 and <= 1500
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "mailto" or "tel"
            && (
                uri.Scheme != "https"
                || (string.IsNullOrEmpty(uri.UserInfo) && !IsWhatsAppHost(uri.IdnHost))
            )
            && sourceContent.Contains(url, StringComparison.OrdinalIgnoreCase)
            ? url
            : "";
    }

    public static bool IsWhatsAppHost(string host) =>
        host.Equals("wa.me", StringComparison.OrdinalIgnoreCase)
        || host.Equals("whatsapp.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".whatsapp.com", StringComparison.OrdinalIgnoreCase);

    public static string ResolveApplyUrl(string? aiUrl, SourceItem item)
    {
        // Always identify the actual collected announcement, never an inferred contact link.
        var originalUrl = item.Url?.Trim() ?? "";
        return
            Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && string.IsNullOrEmpty(uri.UserInfo)
            ? originalUrl
            : "";
    }

    public static List<string> Validate(
        Opportunity o,
        string? sourceContent = null,
        bool allowUnrecognizedLocation = false,
        IEnumerable<string>? approvedLocations = null
    )
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(o.Title))
            errors.Add("Title required.");
        if (string.IsNullOrWhiteSpace(o.Organization))
            errors.Add("Organization required.");
        if (string.IsNullOrWhiteSpace(o.Summary))
            errors.Add("Summary required.");
        if (!Enum.IsDefined(o.Kind))
            errors.Add("Invalid opportunity kind.");
        if (o.OriginalLanguage is not ("ar" or "en"))
            errors.Add("Language must be ar or en.");
        if (
            o.Kind != OpportunityKind.Job
            && (o.EmploymentType.Length > 0 || o.ExperienceLevel.Length > 0)
        )
            errors.Add("Employment fields belong to jobs only.");
        if (o.Locations.Length == 0)
            errors.Add("Location needs review.");
        var localPlaces = new[]
        {
            "غزة",
            "القدس",
            "رام الله",
            "البيرة",
            "الخليل",
            "نابلس",
            "جنين",
            "بيت لحم",
            "أريحا",
            "اريحا",
            "طولكرم",
            "قلقيلية",
            "طوباس",
            "سلفيت",
            "فلسطين",
            "الضفة",
            "Gaza",
            "Jerusalem",
            "Ramallah",
            "Hebron",
            "Nablus",
            "Jenin",
            "Bethlehem",
            "Jericho",
            "Tulkarm",
            "Qalqilya",
            "Tubas",
            "Salfit",
            "Palestine",
            "West Bank",
        };
        if (
            !allowUnrecognizedLocation
            && !localPlaces.Any(place =>
                o.Locations.Contains(place, StringComparison.OrdinalIgnoreCase)
            )
            && !LocationCatalog.Matches(o.Locations, approvedLocations ?? [])
        )
            errors.Add("Local geographic scope needs review.");
        if (
            o.ApplyUrl.Length > 0
            && (
                !Uri.TryCreate(o.ApplyUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("https" or "mailto" or "tel")
            )
        )
            errors.Add("Invalid application link.");
        if (sourceContent is not null && !TextEvidence.TitleContains(sourceContent, o.Title))
            errors.Add("Original title not found in source text.");
        return errors;
    }

    public static bool DateEvidenceMatches(string evidence, DateOnly claimed)
    {
        evidence = string.Concat(
            TextEvidence
                .Normalize(evidence)
                .Select(c =>
                    char.GetUnicodeCategory(c)
                    == System.Globalization.UnicodeCategory.DecimalDigitNumber
                        ? ((int)char.GetNumericValue(c)).ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                        : c.ToString()
                )
        );
        foreach (
            System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                evidence,
                @"(?<![\d/\-])(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?![\d/\-])"
            )
        )
            if (
                DateOnly.TryParseExact(
                    match.Value,
                    new[]
                    {
                        "yyyy-MM-dd",
                        "yyyy-M-d",
                        "yyyy/MM/dd",
                        "yyyy/M/d",
                        "dd/MM/yyyy",
                        "d/M/yyyy",
                        "dd-MM-yyyy",
                        "d-M-yyyy",
                    },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed
                )
                && parsed == claimed
            )
                return true;
        // Extract the date itself: the evidence can include a weekday, deadline label and time.
        string[] monthNames =
        [
            "January|Jan|يناير|كانون الثاني",
            "February|Feb|فبراير|شباط",
            "March|Mar|مارس|اذار",
            "April|Apr|ابريل|نيسان",
            "May|مايو|ايار",
            "June|Jun|يونيو|حزيران",
            "July|Jul|يوليو|تموز",
            "August|Aug|اغسطس|اب",
            "September|Sept|Sep|سبتمبر|ايلول",
            "October|Oct|اكتوبر|تشرين الاول",
            "November|Nov|نوفمبر|تشرين الثاني",
            "December|Dec|ديسمبر|كانون الاول",
        ];
        for (var month = 1; month <= monthNames.Length; month++)
        {
            var name = "(?:" + monthNames[month - 1] + ")";
            var pattern =
                @"\b(?:(?<day>\d{1,2})\s+"
                + name
                + @"\.?[\s,،]+(?<year>\d{4})|"
                + name
                + @"\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?[\s,،]+(?<year>\d{4}))\b";
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    evidence,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                )
            )
                if (
                    int.Parse(
                        match.Groups["day"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    ) == claimed.Day
                    && int.Parse(
                        match.Groups["year"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    ) == claimed.Year
                    && month == claimed.Month
                )
                    return true;
        }

        foreach (
            var culture in new[]
            {
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.CultureInfo.GetCultureInfo("ar-PS"),
            }
        )
            if (
                DateOnly.TryParse(
                    evidence,
                    culture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                    out var parsed
                )
                && parsed == claimed
            )
                return true;
        return false;
    }
}
