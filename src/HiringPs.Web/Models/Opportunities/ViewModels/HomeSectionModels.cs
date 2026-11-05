namespace HiringPs.Web.Models;

public sealed record DeferredHomeSectionModel(string Key, string Url, string Title, bool Lazy = false);
public sealed record HomeStatsSectionModel(HomeStatsDto Stats, HiringPs.Core.OpportunityKind? Kind);
public sealed record HomeBrowseSectionModel(BrowseFacets Facets, HiringPs.Core.OpportunityKind? Kind);
