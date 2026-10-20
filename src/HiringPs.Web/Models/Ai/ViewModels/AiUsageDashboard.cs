using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class AiUsageDashboard
{
    public AiPauseStatusDto PauseStatus { get; set; } = new(null, "");
    public List<AiUsageSummaryDto> Items { get; set; } = [];
    public List<AiUsageTotalsDto> Groups { get; set; } = [];
    public int Days
    {
        get; set;
    }
    public AdminPager ItemsPager { get; set; } = new();
    public AdminPager GroupsPager { get; set; } = new();
}
