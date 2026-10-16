using System.Text.RegularExpressions;

namespace HiringPs.Core;

public record CheckedDates(IReadOnlyList<ExtractedDate> Dates, IReadOnlyList<string> Errors);

public static class DateEvidenceRules
{
    private const string Numeric =
        @"(?<![\d/\-])(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?![\d/\-])";
    private const string Months =
        "January|Jan|February|Feb|March|Mar|April|Apr|May|June|Jun|July|Jul|August|Aug|September|Sept|Sep|October|Oct|November|Nov|December|Dec|يناير|كانون الثاني|فبراير|شباط|مارس|اذار|ابريل|نيسان|مايو|ايار|يونيو|حزيران|يوليو|تموز|اغسطس|اب|سبتمبر|ايلول|اكتوبر|تشرين الاول|نوفمبر|تشرين الثاني|ديسمبر|كانون الاول";
    private const string Roles =
        @"(?<Publication>تاريخ\s+النشر|نشر\s+بتاريخ|publication\s+date|date\s+posted|posted\s+on|published(?:\s+on)?)|(?<Modification>اخر\s+تحديث|تاريخ\s+(?:التعديل|التحديث)|تم\s+(?:التحديث|التعديل)|last\s+(?:updated|modified)|updated\s+on|modified\s+on)|(?<BookletPurchase>شراء\s+(?:كراس[ةه]|وثائق)|(?:كراس[ةه]|وثائق)\s+العطاء|booklet\s+purchase|purchase\s+(?:the\s+)?(?:documents|tender))|(?<SiteVisit>زيار[ةه]\s+(?:الموقع|ميداني[ةه])|site\s+visit)|(?<BidOpening>فتح\s+(?:المظاريف|العروض)|فض\s+العروض|bid\s+opening|opening\s+of\s+(?:bids|tenders))|(?<Deadline>(?:نهاي[ةه]|انتهاء|اغلاق|اقفال)\s+(?:التسجيل|التقديم|استقبال\s+الطلبات)|اخر\s+موعد|الموعد\s+النهائي|موعد\s+(?:الاغلاق|انتهاء|اقفال|التقديم|تقديم\s+العروض)|موعد.{0,30}لتقديم|deadline|closing\s+date|close[sd]?\s+on|apply\s+(?:by|before)|submit.{0,25}(?:by|before)|تمديد\s+موعد|extended\s+(?:to|until))";

    public static CheckedDates Check(ExtractedOpportunity opportunity, string source)
    {
        var entries = new List<ExtractedDate>();
        if (opportunity.PublicationDate is { } published)
            entries.Add(
                new()
                {
                    Kind = OpportunityDateKind.Publication,
                    Date = published,
                    Evidence = opportunity.PublicationEvidence,
                    Label = "Publication",
                }
            );
        if (opportunity.ModificationDate is { } modified)
            entries.Add(
                new()
                {
                    Kind = OpportunityDateKind.Modification,
                    Date = modified,
                    Evidence = opportunity.ModificationEvidence,
                    Label = "Modification",
                }
            );
        if (opportunity.Deadline is { } deadline)
            entries.Add(
                new()
                {
                    Kind = OpportunityDateKind.Deadline,
                    Date = deadline,
                    Evidence = opportunity.DeadlineEvidence,
                    Label = "Deadline",
                }
            );
        entries.AddRange(opportunity.OtherDates ?? []);
        var errors = new List<string>();
        var valid = new List<ExtractedDate>();
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                errors.Add("Date validation: missing date record.");
                continue;
            }
            entry.Evidence ??= "";
            entry.Label ??= "";
            if (
                !Enum.IsDefined(entry.Kind)
                || !TextEvidence.Contains(source, entry.Evidence)
                || !MatchesRole(entry)
            )
            {
                errors.Add(
                    $"Date validation: {entry.Kind} {entry.Date:yyyy-MM-dd} cannot be confirmed from its labeled quotation: {entry.Evidence.Truncate(250)}"
                );
                continue;
            }
            if (
                entry.Kind is OpportunityDateKind.Publication or OpportunityDateKind.Modification
                && entry.Date > TextRules.PalestineToday().AddDays(1)
            )
            {
                errors.Add($"Date validation: {entry.Kind} is in the future.");
                continue;
            }
            valid.Add(entry);
        }
        foreach (
            var group in valid
                .Where(x =>
                    x.Kind
                        is OpportunityDateKind.Publication
                            or OpportunityDateKind.Modification
                            or OpportunityDateKind.Deadline
                )
                .GroupBy(x => x.Kind)
                .Where(x => x.Select(d => d.Date).Distinct().Count() > 1)
        )
        {
            errors.Add($"Date validation: conflicting values for {group.Key}.");
            valid.RemoveAll(x => x.Kind == group.Key);
        }
        if (opportunity.DeadlineIsExtension && !HasExtension(opportunity.DeadlineEvidence))
        {
            errors.Add("Date validation: a deadline extension has no explicit extension evidence.");
            valid.RemoveAll(x => x.Kind == OpportunityDateKind.Deadline);
        }
        var pub = valid.FirstOrDefault(x => x.Kind == OpportunityDateKind.Publication);
        var close = valid.FirstOrDefault(x => x.Kind == OpportunityDateKind.Deadline);
        if (pub is not null && close is not null && close.Date < pub.Date)
        {
            errors.Add("Date validation: closing date precedes the publication date.");
            valid.RemoveAll(x => x.Kind == OpportunityDateKind.Deadline);
        }
        return new(valid.DistinctBy(x => (x.Kind, x.Date, x.Evidence)).ToList(), errors);
    }

    public static bool HasExtension(string evidence) =>
        Regex.IsMatch(
            TextEvidence.Normalize(evidence),
            @"تمديد|مدد|extended|extension",
            RegexOptions.IgnoreCase
        );

    private static bool MatchesRole(ExtractedDate entry)
    {
        var quote = TextEvidence.Normalize(entry.Evidence);
        var dates = Regex.Matches(
            quote,
            Numeric
                + @"|\d{1,2}\s+(?:"
                + Months
                + @")\.?[\s,،]+\d{4}|(?:"
                + Months
                + @")\.?\s+\d{1,2}(?:st|nd|rd|th)?[\s,،]+\d{4}",
            RegexOptions.IgnoreCase
        );
        foreach (Match date in dates)
        {
            if (!OpportunityService.DateEvidenceMatches(date.Value, entry.Date))
                continue;
            var preceding = quote[..date.Index];
            var role = Regex
                .Matches(preceding, Roles, RegexOptions.IgnoreCase)
                .Cast<Match>()
                .LastOrDefault();
            if (entry.Kind == OpportunityDateKind.Other)
            {
                if (
                    role is null
                    && entry.Label.Length >= 4
                    && TextEvidence.Contains(quote, entry.Label)
                )
                    return true;
                continue;
            }
            if (
                role is not null
                && date.Index - role.Index <= 160
                && role.Groups[entry.Kind.ToString()].Success
            )
                return true;
        }
        return false;
    }

    public static DateTimeOffset AtPalestineMidnight(DateOnly date) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), SourceSchedule.Zone));
}
