using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public static class LocationCatalog
{
    public static string[] Split(string locations) => locations.Split([',', '،', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    public static async Task<List<string>> ApprovedAsync(HiringDb db, CancellationToken ct)
    {
        var recorded = await db.Audit.AsNoTracking().Where(x => x.Action == "Location.Approved").Select(x => x.Detail).ToListAsync(ct);
        // Existing manual approvals are included without a destructive reseed/backfill.
        var previous = await db.Opportunities.AsNoTracking().Where(x => x.AdminOverride && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived)).Select(x => x.Locations).Distinct().ToListAsync(ct);
        return recorded.Concat(previous).SelectMany(Split).Where(x => x.Length <= 200).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }
    public static bool Matches(string location, IEnumerable<string> approved) => Split(location).Any(part => approved.Any(x => string.Equals(TextRules.Normalize(part), TextRules.Normalize(x), StringComparison.Ordinal)));
}
