using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class PreferenceInput
{
    public OpportunityKind[] Kinds { get; set; } = [];
    public string[] Categories { get; set; } = [];
    public string[] Locations { get; set; } = [];
    public string[] AvailableCategories { get; set; } = [];
    public string[] AvailableLocations { get; set; } = [];
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public CategoryGroupMap CategoryGroups { get; set; } = CategoryGroupMap.Empty;
    public bool SavedReminders { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public int? CategoryLimit { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool LimitExceeded { get; set; }
}
