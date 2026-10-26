using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>Applies the same public search and filters to listings and RSS.</summary>
public static class PublicListingFilters
{
    /// <summary>Selects current or archived opportunities using the Palestine calendar date.</summary>
    public static IQueryable<Opportunity> Scope(IQueryable<Opportunity> query, bool archive, DateOnly today) =>
        PublicOpportunityScope.Listing(query, archive, today);

    /// <summary>Filters a query without changing access permissions or executing a database request.</summary>
    public static IQueryable<Opportunity> Apply(IQueryable<Opportunity> query, PublicListingFilterDto input, FilterValues filters)
    {
        var (q, kind, city, category, employment, experience) = input;
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().Truncate(200);
            foreach (
                var term in LocationSearch
                    .Normalize(q)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct()
                    .Take(12)
            )
                query = query.Where(x => x.SearchText.Contains(term));
        }

        query = PublicListingKinds.Apply(query, kind);
        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = CityCatalog.Find(city)?.SearchKey ?? LocationSearch.Normalize(city);
            query = query.Where(x => x.LocationSearchText.Contains(normalizedCity));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categories = FilterLabels.Equivalents(category, filters.Categories, FilterLabels.Category);
            query = query.Where(x => categories.Contains(x.Category));
        }
        if (kind == OpportunityKind.Job)
        {
            if (!string.IsNullOrWhiteSpace(employment))
            {
                var types = FilterLabels.Equivalents(
                    employment,
                    filters.EmploymentTypes,
                    FilterLabels.Employment
                );
                query = query.Where(x => types.Contains(x.EmploymentType));
            }
            if (!string.IsNullOrWhiteSpace(experience))
            {
                var levels = FilterLabels.Equivalents(
                    experience,
                    filters.ExperienceLevels,
                    FilterLabels.Experience
                );
                query = query.Where(x => levels.Contains(x.ExperienceLevel));
            }
        }

        return query;
    }
}
