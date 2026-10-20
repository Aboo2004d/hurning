using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads reports and records their resolution with the existing audit trail.</summary>
public sealed class ReportManagementService(HiringDb db)
{
    /// <summary>Projects reports in unresolved-first and newest-id order before pagination.</summary>
    public IQueryable<UserReportSummaryDto> Reports() => db.Reports.AsNoTracking()
        .OrderBy(x => x.Resolved).ThenByDescending(x => x.Id)
        .Select(x => new UserReportSummaryDto(x.Id, x.OpportunityId, x.Opportunity.Title, x.Reason, x.Details, x.Resolved));

    /// <summary>Marks an existing report resolved; does not alter publication or subscription state.</summary>
    public async Task<bool> ResolveAsync(long id, string actor, CancellationToken ct)
    {
        var report = await db.Reports.FindAsync([id], ct);
        if (report is null)
            return false;
        report.Resolved = true;
        db.Audit.Add(new()
        {
            Actor = actor,
            Action = "Report.Resolve",
            Detail = $"Report {id}"
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
