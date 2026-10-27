using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>مكون DateEvidenceRules ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class DateEvidenceRules
{
    private const string Numeric =
        @"(?<![\d/\-])(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?![\d/\-])";
    private const string Months =
        "January|Jan|February|Feb|March|Mar|April|Apr|May|June|Jun|July|Jul|August|Aug|September|Sept|Sep|October|Oct|November|Nov|December|Dec|يناير|كانون الثاني|فبراير|شباط|مارس|اذار|ابريل|نيسان|مايو|ايار|يونيو|حزيران|يوليو|تموز|اغسطس|اب|سبتمبر|ايلول|اكتوبر|تشرين الاول|نوفمبر|تشرين الثاني|ديسمبر|كانون الاول";
    private const string Roles =
        @"(?<Publication>تاريخ\s+النشر|نشر\s+بتاريخ|publication\s+date|date\s+posted|posted\s+on|published(?:\s+on)?)|(?<Modification>اخر\s+تحديث|تاريخ\s+(?:التعديل|التحديث)|تم\s+(?:التحديث|التعديل)|last\s+(?:updated|modified)|updated\s+on|modified\s+on)|(?<BookletPurchase>شراء\s+(?:كراس[ةه]|وثائق)|(?:كراس[ةه]|وثائق)\s+العطاء|booklet\s+purchase|(?:purchase|obtain|collect|collecting|obtaining|purchasing)\s+(?:the\s+)?(?:tender\s+documents|bidding\s+documents|documents|tender)|(?:الحصول|استلام)\s+(?:عل[ىي]\s+)?(?:كراس[ةه]|وثائق|مستندات))|(?<SiteVisit>زيار[ةه]\s+(?:الموقع|ميداني[ةه])|site\s+visit)|(?<BidOpening>فتح\s+(?:المظاريف|العروض)|فض\s+العروض|bid\s+opening|opening\s+of\s+(?:bids|tenders))|(?<Deadline>(?:نهاي[ةه]|انتهاء|اغلاق|اقفال)\s+(?:التسجيل|التقديم|استقبال\s+الطلبات)|اخر\s+موعد|الموعد\s+النهائي|موعد\s+(?:الاغلاق|انتهاء|اقفال|التقديم|تقديم\s+العروض)|موعد.{0,30}لتقديم|deadline|closing\s+date|close[sd]?\s+on|apply\s+(?:by|before)|submit.{0,25}(?:by|before)|تمديد\s+موعد|extended\s+(?:to|until))";

    // These are conflicting events, not an allowlist of closing expressions.
    private const string NonClosingEvents =
        @"(?<NonClosing>نهاي[ةه]\s+(?:التدريب|الدور[ةه]|المشروع|العقد)|بداي[ةه]\s+(?:التسجيل|التقديم|التدريب)|(?:training|course|project|contract)\s+(?:end|ends|ending)|(?:registration|applications?)\s+(?:open|opens|opening))";

    /// <summary>ينفذ Check وفق قواعد DateEvidenceRules الحالية دون تغيير عقد المستدعين.</summary>
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
        foreach (var extracted in entries)
        {
            var entry = extracted;
            if (entry is null)
            {
                errors.Add("Date validation: missing date record.");
                continue;
            }
            entry.Evidence ??= "";
            entry.Label ??= "";
            // Preserve a verified opening event without accepting it as publication.
            // Work on a copy so the original model response remains available for audit.
            if (entry.Kind == OpportunityDateKind.Publication
                && TextEvidence.Contains(source, entry.Evidence)
                && IsApplicationOpening(entry)
                && !MatchesRole(entry))
            {
                entry = new ExtractedDate
                {
                    Kind = OpportunityDateKind.Other,
                    Date = entry.Date,
                    Label = entry.Evidence,
                    Evidence = entry.Evidence,
                };
            }
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

    /// <summary>ينفذ HasExtension وفق قواعد DateEvidenceRules الحالية دون تغيير عقد المستدعين.</summary>
    public static bool HasExtension(string evidence) =>
        Regex.IsMatch(
            TextEvidence.Normalize(evidence),
            @"تمديد|مدد|extended|extension",
            RegexOptions.IgnoreCase
        );

    /// <summary>ينفذ MatchesRole وفق قواعد DateEvidenceRules الحالية دون تغيير عقد المستدعين.</summary>
    private static bool MatchesRole(ExtractedDate entry)
    {
        var quote = TextEvidence.Normalize(entry.Evidence);
        var dates = QuotedDates(quote);
        foreach (Match date in dates)
        {
            if (!ExtractionValidator.DateEvidenceMatches(date.Value, entry.Date))
                continue;
            if (entry.Kind is OpportunityDateKind.Deadline or OpportunityDateKind.BookletPurchase
                && IsOpeningDate(quote, dates, date))
                continue;
            var preceding = quote[..date.Index];
            var role = Regex
                .Matches(
                    preceding,
                    entry.Kind == OpportunityDateKind.Deadline
                        ? Roles + "|" + NonClosingEvents
                        : Roles,
                    RegexOptions.IgnoreCase
                )
                .Cast<Match>()
                .LastOrDefault();
            // The model classifies closing meaning semantically. Source quotation
            // and exact date still have to match; reject explicit conflicting events.
            if (entry.Kind == OpportunityDateKind.Deadline)
            {
                if (role is null || role.Groups[nameof(OpportunityDateKind.Deadline)].Success)
                    return true;
                continue;
            }
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

    /// <summary>يثبت بداية استقبال الطلبات من الاقتباس وقيمته، دون تحويل نهاية النطاق إلى بداية.</summary>
    private static bool IsApplicationOpening(ExtractedDate entry)
    {
        var quote = TextEvidence.Normalize(entry.Evidence);
        if (!Regex.IsMatch(quote,
            @"(?:يبدا|تبدا|بدء|بداي[ةه])\s+(?:موعد\s+)?(?:استقبال|تلقي|تقديم)\s+الطلبات|(?:applications?|registration)\s+(?:open|opens|opening|start|starts)",
            RegexOptions.IgnoreCase))
            return false;
        var dates = QuotedDates(quote);
        return dates.Cast<Match>().Any(date =>
            ExtractionValidator.DateEvidenceMatches(date.Value, entry.Date)
            && IsOpeningDate(quote, dates, date));
    }

    /// <summary>يحدد التواريخ الصريحة في الاقتباس بالقواعد نفسها لجميع أنواع الأحداث.</summary>
    private static MatchCollection QuotedDates(string quote) => Regex.Matches(
        quote,
        Numeric + @"|\d{1,2}\s+(?:" + Months
            + @")\.?[\s,،]+\d{4}|(?:" + Months
            + @")\.?\s+\d{1,2}(?:st|nd|rd|th)?[\s,،]+\d{4}",
        RegexOptions.IgnoreCase);

    /// <summary>يرفض تاريخ بداية الإجراء عند استخدامه كنهاية، دون افتراض أن آخر تاريخ في النص هو الصحيح.</summary>
    private static bool IsOpeningDate(string quote, MatchCollection dates, Match date)
    {
        var previous = dates.Cast<Match>().LastOrDefault(x => x.Index < date.Index);
        var start = previous is null ? 0 : previous.Index + previous.Length;
        var context = quote[start..date.Index];
        // Use the closest temporal cue, so "applications close ... starting from"
        // cannot turn the beginning of a range into its closing date.
        var cues = Regex.Matches(context,
            @"(?<Opening>starting\s+from|starting|starts?\s+(?:on|from)|begin(?:s|ning)?(?:\s+(?:on|from))?|(?:اعتبارا|ابتداء)\s+من|بدء|يبدا|تبدأ|تبدا|بداي[ةه]|(?<!\p{L})و?من(?!\p{L})(?:\s+يوم)?)|(?<Closing>until|through|no\s+later\s+than|on\s+or\s+before|before|by|closing|deadline|ل?غاي[ةه]|حتي|حتى|اخر\s+موعد|اقصاه|ينتهي|تنتهي)",
            RegexOptions.IgnoreCase).Cast<Match>().ToList();
        // English cues must be words; Arabic conjunctions may be attached.
        var cue = cues.LastOrDefault(x =>
            (x.Index == 0 || !char.IsAsciiLetter(context[x.Index - 1]))
            && (x.Index + x.Length == context.Length
                || !char.IsAsciiLetter(context[x.Index + x.Length]))
        );
        return cue is not null && cue.Groups["Opening"].Success;
    }

    /// <summary>ينفذ AtPalestineMidnight وفق قواعد DateEvidenceRules الحالية دون تغيير عقد المستدعين.</summary>
    public static DateTimeOffset AtPalestineMidnight(DateOnly date)
    {
        // Palestinian DST can start at midnight; use the first valid local minute of that day.
        var local = date.ToDateTime(TimeOnly.MinValue);
        for (var m = 0; SourceSchedule.Zone.IsInvalidTime(local) && m < 180; m++)
            local = local.AddMinutes(1);
        return new(TimeZoneInfo.ConvertTimeToUtc(local, SourceSchedule.Zone));
    }
}
