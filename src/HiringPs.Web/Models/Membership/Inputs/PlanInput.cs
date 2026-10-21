using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class PlanInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string NameAr { get; set; } = "";

    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string NameEn { get; set; } = "";

    [StringLength(2000)]
    public string DescriptionAr { get; set; } = "";

    [StringLength(2000)]
    public string DescriptionEn { get; set; } = "";

    [StringLength(80)]
    public string BadgeAr { get; set; } = "";

    [StringLength(80)]
    public string BadgeEn { get; set; } = "";

    [Range(
        typeof(decimal),
        "0.01",
        "100000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true
    )]
    [Microsoft.AspNetCore.Mvc.ModelBinder(
        BinderType = typeof(HiringPs.Web.Infrastructure.InvariantDecimalModelBinder)
    )]
    public decimal PriceIls { get; set; } = 5;

    [Range(1, 3650)]
    public int Days { get; set; } = 30;
    [Range(1, 1000, ErrorMessage = "PreferredCategoryLimitRange")]
    public int? MaxPreferredCategories { get; set; }
    public OpportunityKind[] Details { get; set; } = [];
    public OpportunityKind[] Alerts { get; set; } = [];
    public bool Visible { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Version { get; set; }
}
