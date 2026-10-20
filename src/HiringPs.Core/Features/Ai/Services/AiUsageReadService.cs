using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Projects usage rows and aggregates known token counts with the original null-count semantics.</summary>
public sealed class AiUsageReadService(HiringDb db)
{
    /// <summary>Restricts usage to the requested rolling UTC interval; display conversion is independent.</summary>
    private IQueryable<AiUsage> Query(DateTimeOffset since) => db.AiUsage.AsNoTracking().Where(x => x.StartedAt >= since);

    /// <summary>Returns newest invocation summaries before pagination.</summary>
    public IQueryable<AiUsageSummaryDto> Requests(DateTimeOffset since) => Query(since).OrderByDescending(x => x.Id)
        .Select(x => new AiUsageSummaryDto(x.Id, x.SourceItemId, x.PromptVersionId, x.Model, x.Success, x.ErrorType, x.HttpStatus, x.TotalTokens, x.DurationMs, x.StartedAt));

    /// <summary>Groups requests by prompt and model; totals exclude unknown counts instead of treating them as known zeroes.</summary>
    public IQueryable<AiUsageTotalsDto> Totals(DateTimeOffset since)
    {
        return Query(since)
            .GroupBy(x => new { x.PromptVersionId, x.Model })
            .Select(g => new AiUsageTotalsDto
            {
                PromptVersionId = g.Key.PromptVersionId,
                Model = g.Key.Model,
                Requests = g.Count(),
                Succeeded = g.Count(x => x.Success == true),
                Failed = g.Count(x => x.Success == false),
                Unfinished = g.Count(x => x.Success == null),
                UnknownTokenRequests = g.Count(x => x.TotalTokens == null),
                InputTokens =
                    g.Count(x => x.InputTokens != null) > 0
                        ? g.Sum(x => (long?)x.InputTokens)
                        : null,
                OutputTokens =
                    g.Count(x => x.OutputTokens != null) > 0
                        ? g.Sum(x => (long?)x.OutputTokens)
                        : null,
                TotalTokens =
                    g.Count(x => x.TotalTokens != null) > 0
                        ? g.Sum(x => (long?)x.TotalTokens)
                        : null,
            });
    }
}
