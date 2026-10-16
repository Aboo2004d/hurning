using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class ToolDashboard
{
    public ToolWorkerState? Worker { get; set; }
    public int FreshWorkers { get; set; }
    public List<ToolCheckResult> Results { get; set; } = [];
    public List<ToolCheckRequest> Requests { get; set; } = [];
    public AiUsage? LastAi { get; set; }
    public ScrapeRun? LastRun { get; set; }
    public int Waiting { get; set; }
    public int Review { get; set; }
    public int EnabledSources { get; set; }
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
}
