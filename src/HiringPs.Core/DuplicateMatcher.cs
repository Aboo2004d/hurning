// مطابقة التكرار: المرجع الحتمي بعد توحيد اسم الجهة، والتشابه القريب الذي يُعرض على المسؤول.
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public static class DuplicateMatcher
{
    public const double TitleSimilarity = 0.6;
    public const int DeadlineToleranceDays = 14;

    // Reference numbers differ only by spacing, separators, case or digit script across sources.
    public static string ReferenceKey(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return "";
        var key = new StringBuilder();
        foreach (var c in reference.Normalize(NormalizationForm.FormKC))
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber)
                key.Append((char)('0' + (int)char.GetNumericValue(c)));
            else if (char.IsLetter(c))
                key.Append(char.ToUpperInvariant(c));
        }
        // "~" marks a reference with no usable characters so it never matches another record.
        return key.Length == 0 ? "~" : key.ToString().Truncate(200);
    }

    public static bool UsableReference(string key) => key.Length > 0 && key != "~";

    public static double TitleScore(string a, string b)
    {
        var left = TextRules.Normalize(a).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var right = TextRules.Normalize(b).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (left.Count == 0 || right.Count == 0)
            return 0;
        return (double)left.Intersect(right).Count() / left.Union(right).Count();
    }

    public static bool DeadlinesOverlap(DateOnly? a, DateOnly? b) =>
        a is null || b is null || Math.Abs(a.Value.DayNumber - b.Value.DayNumber) <= DeadlineToleranceDays;

    // Same kind and organisation are checked by the caller (alias-aware query) or here by name.
    public static bool PotentialDuplicate(
        Opportunity incoming,
        Opportunity existing,
        bool sameOrganization = false
    )
    {
        if (
            incoming.Kind != existing.Kind
            || (
                !sameOrganization
                && TextRules.Normalize(incoming.Organization)
                    != TextRules.Normalize(existing.Organization)
            )
        )
            return false;
        if (incoming.ReferenceNumber.Length > 0 && existing.ReferenceNumber.Length > 0)
            return ReferenceKey(incoming.ReferenceNumber) == ReferenceKey(existing.ReferenceNumber);
        return TitleScore(incoming.Title, existing.Title) >= TitleSimilarity;
    }

    // A near match worth an administrator's comparison: similar title and compatible deadline.
    public static (double Score, string Reason)? NearMatch(Opportunity incoming, Opportunity existing)
    {
        if (!PotentialDuplicate(incoming, existing, sameOrganization: true))
            return null;
        if (!DeadlinesOverlap(incoming.Deadline, existing.Deadline))
            return null;
        var sameReference =
            UsableReference(ReferenceKey(incoming.ReferenceNumber))
            && ReferenceKey(incoming.ReferenceNumber) == ReferenceKey(existing.ReferenceNumber);
        var score = sameReference ? 1 : Math.Round(TitleScore(incoming.Title, existing.Title), 3);
        return (
            score,
            sameReference
                ? "Same organisation and reference number."
                : $"Same organisation and kind; title similarity {score:0.00}; compatible deadline."
        );
    }

    public sealed class AliasMap(IReadOnlyDictionary<string, string> canonicalByAlias)
    {
        public string Canonical(string key) =>
            canonicalByAlias.TryGetValue(key, out var canonical) ? canonical : key;

        // Every stored spelling that resolves to the same canonical organisation.
        public string[] Keys(string key)
        {
            var canonical = Canonical(key);
            return canonicalByAlias
                .Where(x => x.Value == canonical)
                .Select(x => x.Key)
                .Append(canonical)
                .Append(key)
                .Distinct()
                .ToArray();
        }
    }

    public static async Task<AliasMap> AliasesAsync(HiringDb db, CancellationToken ct) =>
        new(
            await db
                .OrganizationAliases.AsNoTracking()
                .ToDictionaryAsync(x => x.Alias, x => x.Canonical, ct)
        );

    public record ReferenceMatch(Opportunity? Exact, IReadOnlyList<long> Ambiguous);

    // Deterministic cross-source identity: kind + canonical organisation + normalised reference.
    // Several matches (e.g. tender lots sharing one reference) are ambiguous and never auto-merged.
    public static async Task<ReferenceMatch> ExactReferenceAsync(
        HiringDb db,
        Opportunity candidate,
        string[] organizationKeys,
        long[] excludedIds,
        CancellationToken ct
    )
    {
        var reference = ReferenceKey(candidate.ReferenceNumber);
        if (!UsableReference(reference) || candidate.Organization.Length == 0)
            return new(null, []);
        var raw = candidate.ReferenceNumber;
        var matches = await db
            .Opportunities.Where(x =>
                x.Kind == candidate.Kind
                && organizationKeys.Contains(x.NormalizedOrganization)
                && x.Status != PublicationStatus.Hidden
                && !excludedIds.Contains(x.Id)
                // Records saved before NormalizedReference existed keep an empty key.
                && (
                    x.NormalizedReference == reference
                    || (x.NormalizedReference == "" && x.ReferenceNumber == raw)
                )
            )
            .OrderBy(x => x.Id)
            .Take(5)
            .ToListAsync(ct);
        return matches.Count == 1
            ? new(matches[0], [])
            : new(null, matches.Select(x => x.Id).ToList());
    }

    // Records each candidate pair once; a dismissed pair is never reopened automatically.
    public static async Task RecordAsync(
        HiringDb db,
        IEnumerable<(Opportunity Opportunity, long CandidateId, double Score, string Reason)> pairs,
        CancellationToken ct
    )
    {
        var list = pairs.Where(x => x.Opportunity.Id != 0 && x.Opportunity.Id != x.CandidateId).ToList();
        if (list.Count == 0)
            return;
        var ids = list.Select(x => x.Opportunity.Id).Distinct().ToArray();
        var existing = (
            await db
                .DuplicateCandidates.AsNoTracking()
                .Where(x => ids.Contains(x.OpportunityId) || ids.Contains(x.CandidateId))
                .Select(x => new { x.OpportunityId, x.CandidateId })
                .ToListAsync(ct)
        )
            .SelectMany(x => new[] { (x.OpportunityId, x.CandidateId), (x.CandidateId, x.OpportunityId) })
            .ToHashSet();
        foreach (var pair in list.DistinctBy(x => (x.Opportunity.Id, x.CandidateId)))
        {
            if (!existing.Add((pair.Opportunity.Id, pair.CandidateId)))
                continue;
            db.DuplicateCandidates.Add(
                new()
                {
                    OpportunityId = pair.Opportunity.Id,
                    CandidateId = pair.CandidateId,
                    Score = pair.Score,
                    Reason = pair.Reason.Truncate(300),
                }
            );
        }
    }
}
