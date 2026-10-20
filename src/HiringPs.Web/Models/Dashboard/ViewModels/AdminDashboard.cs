using HiringPs.Core;
using HiringPs.Web.Models;

namespace HiringPs.Web.Controllers;

public class AdminDashboard
{
    public int Published
    {
        get; set;
    }
    public int Review
    {
        get; set;
    }
    public int Queued
    {
        get; set;
    }
    public int Reports
    {
        get; set;
    }
    public WorkerHeartbeatSummaryDto? Heartbeat
    {
        get; set;
    }
    public List<ScrapeRunSummaryDto> Runs { get; set; } = [];
    public AdminPager RunsPager { get; set; } = new();
    public List<SourceItemSummaryDto> Items { get; set; } = [];
    public AdminPager ItemsPager { get; set; } = new();
}
