using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>Search criteria shared by the home page and its RSS feed.</summary>
public sealed record PublicListingFilterDto(string? Query, OpportunityKind? Kind, string? City, string? Category, string? Employment, string? Experience);
