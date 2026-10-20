using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class PromptEditor
{
    [Required, MinLength(100), MaxLength(30000)]
    public string Template { get; set; } = "";

    [Range(40, 2000)]
    public int SummaryLimit { get; set; } = 200;

    [Required, MaxLength(500)]
    public string GeographicScope { get; set; } = "";

    [Required, MaxLength(200)]
    public string AllowedKinds { get; set; } = "";
    public bool AllowUnknownDeadline { get; set; } = true;

    [MaxLength(500)]
    public string Note { get; set; } = "";
    public string Version { get; set; } = "";
    public PromptSettingsDto? Settings
    {
        get; set;
    }
    public List<PromptVersionSummaryDto> History { get; set; } = [];
    public AdminPager HistoryPager { get; set; } = new();
}
