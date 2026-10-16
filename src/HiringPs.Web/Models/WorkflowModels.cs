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
    public WorkflowSettings? Settings { get; set; }
    public List<PromptVersion> History { get; set; } = [];
    public AdminPager HistoryPager { get; set; } = new();
}

public class RetentionEditor
{
    public bool Enabled { get; set; }

    [Range(7, 3650)]
    public int RevisionDays { get; set; }

    [Range(7, 3650)]
    public int DiagnosticDays { get; set; }
    public string Version { get; set; } = "";
    public RetentionPolicy? Current { get; set; }
}

public class AiUsageDashboard
{
    public List<AiUsage> Items { get; set; } = [];
    public List<AiUsageTotals> Groups { get; set; } = [];
    public int Days { get; set; }
    public AdminPager ItemsPager { get; set; } = new();
    public AdminPager GroupsPager { get; set; } = new();
}

public class AiUsageTotals
{
    public int PromptVersionId { get; set; }
    public string Model { get; set; } = "";
    public int Requests { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Unfinished { get; set; }
    public int UnknownTokenRequests { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
}
