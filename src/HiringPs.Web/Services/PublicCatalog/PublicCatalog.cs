// قوائم الفلاتر وصفحات التصفح (المدن والجهات) مخزنة مؤقتًا في الذاكرة لعشر دقائق.
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HiringPs.Web.Models;

/// <summary>مكون PublicCatalog ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed class PublicCatalog(HiringDb db, IMemoryCache cache, PublicCatalogCache? coherence = null, PublicCatalogRevisionMonitor? revisionMonitor = null)
{
    private readonly PublicCatalogCache coherence = coherence ?? new();

    private async Task<T> CachedAsync<T>(string key, Func<ICacheEntry, Task<T>> create)
    {
        if (revisionMonitor is not null) await revisionMonitor.RefreshAsync(CancellationToken.None);
        var snapshot = coherence.Snapshot();
        return (await cache.GetOrCreateAsync(key + ":" + snapshot.Version, entry =>
        {
            entry.AddExpirationToken(snapshot.Changed);
            return create(entry);
        }))!;
    }

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>ينفذ FiltersAsync وفق قواعد PublicCatalog الحالية دون تغيير عقد المستدعين.</summary>
    public Task<FilterValues> FiltersAsync(OpportunityKind? kind, bool archive, CancellationToken ct) =>
        CachedAsync(
            $"filters:{kind}:{archive}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Lifetime;
                var today = TextRules.PalestineToday();
                var query = db
                    .Opportunities.AsNoTracking()
                    .Where(x =>
                        archive
                            ? x.Status == PublicationStatus.Archived
                                || (
                                    x.Status == PublicationStatus.Published
                                    && x.Deadline != null
                                    && x.Deadline < today
                                )
                            : x.Status == PublicationStatus.Published
                                && (x.Deadline == null || x.Deadline >= today)
                    );
                query = PublicListingKinds.Apply(query, kind);
                var categories = await query
                    .Where(x => x.Category != "")
                    .Select(x => x.Category)
                    .Distinct()
                    .ToListAsync(ct);
                var employment = await query
                    .Where(x => x.Kind == OpportunityKind.Job && x.EmploymentType != "")
                    .Select(x => x.EmploymentType)
                    .Distinct()
                    .ToListAsync(ct);
                var experience = await query
                    .Where(x => x.Kind == OpportunityKind.Job && x.ExperienceLevel != "")
                    .Select(x => x.ExperienceLevel)
                    .Distinct()
                    .ToListAsync(ct);
                return new FilterValues(categories, employment, experience, await LocationsAsync(ct));
            }
        )!;

    /// <summary>Catalog cities plus admin-approved locations; also the member preference location catalog.</summary>
    public Task<List<string>> LocationsAsync(CancellationToken ct) =>
        CachedAsync(
            "locations",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Lifetime;
                return CityCatalog
                    .ArabicNames.Concat(await LocationCatalog.ApprovedAsync(db, ct))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();
            }
        )!;

    /// <summary>ينفذ PublicDetailKindsAsync وفق قواعد PublicCatalog الحالية دون تغيير عقد المستدعين.</summary>
    public Task<HashSet<OpportunityKind>> PublicDetailKindsAsync(CancellationToken ct) =>
        CachedAsync(
            "public-detail-kinds",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return (
                    await db
                        .OpportunityAccessRules.AsNoTracking()
                        .Where(x => x.PublicDetails)
                        .Select(x => x.Kind)
                        .ToListAsync(ct)
                ).ToHashSet();
            }
        )!;

    /// <summary>Kind × city and organization pages that currently have at least one public opportunity.</summary>
    public Task<BrowseFacets> FacetsAsync(CancellationToken ct) =>
        CachedAsync(
            "browse-facets",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Lifetime;
                var rows = await db
                    .Opportunities.AsNoTracking()
                    .Current(TextRules.PalestineToday())
                    .Select(x => new
                    {
                        x.Kind,
                        x.LocationSearchText,
                        x.NormalizedOrganization,
                        x.Organization,
                        Changed = x.SourceModifiedAt ?? x.SourcePublishedAt ?? x.CreatedAt,
                    })
                    .ToListAsync(ct);
                var cities = (
                    from row in rows
                    from city in CityCatalog.All
                    where row.LocationSearchText.Contains(city.SearchKey, StringComparison.Ordinal)
                    group row by (row.Kind, city) into g
                    select new CityFacet(g.Key.Kind, g.Key.city, g.Count(), g.Max(x => x.Changed))
                )
                    .OrderBy(x => x.Kind)
                    .ThenByDescending(x => x.Count)
                    .ToList();
                var organizations = rows.Where(x => x.NormalizedOrganization.Length > 0)
                    .GroupBy(x =>
                        UrlSlug.Create(
                            x.NormalizedOrganization.StartsWith("sha256:", StringComparison.Ordinal)
                                ? x.Organization
                                : x.NormalizedOrganization,
                            80
                        )
                    )
                    .Where(g => g.Key.Length > 0)
                    .Select(g => new OrganizationFacet(
                        g.Key,
                        g.GroupBy(x => x.Organization).OrderByDescending(x => x.Count()).First().Key,
                        g.Select(x => x.NormalizedOrganization).Distinct().ToArray(),
                        g.Count(),
                        g.Max(x => x.Changed)
                    ))
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .ToList();
                return new BrowseFacets(cities, organizations);
            }
        )!;

    /// <summary>Current counts per kind and organizations for the home page header.</summary>
    public Task<HomeStatsDto> StatsAsync(CancellationToken ct) =>
        CachedAsync(
            "home-stats",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Lifetime;
                var current = db.Opportunities.AsNoTracking().Current(TextRules.PalestineToday());
                var counts = await current
                    .GroupBy(x => x.Kind)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
                var organizations = await current
                    .Where(x => x.NormalizedOrganization != "")
                    .Select(x => x.NormalizedOrganization)
                    .Distinct()
                    .CountAsync(ct);
                var since = DateTimeOffset.UtcNow.AddDays(-1);
                var fresh = await current.CountAsync(x => x.CreatedAt >= since, ct);
                return new HomeStatsDto(counts, organizations, fresh);
            }
        )!;
}
