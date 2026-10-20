using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads content snapshots and saves versioned edits without exposing tracked SitePage entities.</summary>
public sealed class SiteContentService(HiringDb db)
{
    /// <summary>Reads managed-page metadata; missing keys continue to use localized built-in content.</summary>
    public Task<List<SitePageSummaryDto>> PagesAsync(CancellationToken ct) => db.SitePages.AsNoTracking()
        .Select(x => new SitePageSummaryDto(x.Key, x.UpdatedAt)).ToListAsync(ct);

    /// <summary>Reads one immutable content snapshot and its edit token.</summary>
    public Task<SitePageContentDto?> ReadAsync(string key, CancellationToken ct) => db.SitePages.AsNoTracking()
        .Where(x => x.Key == key).Select(x => new SitePageContentDto(x.Key, x.TitleAr, x.BodyAr, x.TitleEn, x.BodyEn, x.Version))
        .SingleOrDefaultAsync(ct);

    /// <summary>Saves only ordinary editable pages and retains their original audit action.</summary>
    public Task<RecordUpdateOutcome> SavePageAsync(SitePageWriteDto input, string actor, CancellationToken ct) =>
        SitePageKeys.IsEditable(input.Key)
            ? SaveAsync(input, actor, "SitePage.Update", $"Page {input.Key}", ct)
            : Task.FromResult(RecordUpdateOutcome.NotFound);

    /// <summary>Saves the bilingual questions JSON in BodyAr, preserving the historical storage contract.</summary>
    public Task<RecordUpdateOutcome> SaveQuestionsAsync(SitePageWriteDto input, int questionCount, string actor, CancellationToken ct) =>
        input.Key == SitePageKeys.Questions
            ? SaveAsync(input with
            {
                BodyEn = null
            }, actor, "SiteQuestions.Update", $"Questions {questionCount}", ct)
            : Task.FromResult(RecordUpdateOutcome.NotFound);

    /// <summary>Rejects missing/stale edit tokens and translates rowversion or unique-key races into an edit conflict.</summary>
    private async Task<RecordUpdateOutcome> SaveAsync(SitePageWriteDto input, string actor, string action, string detail, CancellationToken ct)
    {
        var page = await db.SitePages.SingleOrDefaultAsync(x => x.Key == input.Key, ct);
        if ((page is not null && (input.Version is null || input.Version.Length == 0))
            || (page is null && input.Version is { Length: > 0 }))
            return RecordUpdateOutcome.Conflict;
        if (page is null)
        {
            page = new SitePage { Key = input.Key };
            db.SitePages.Add(page);
        }
        else
            db.Entry(page).Property(x => x.Version).OriginalValue = input.Version!;
        page.TitleAr = input.TitleAr.Trim();
        page.TitleEn = input.TitleEn.Trim();
        page.BodyAr = input.BodyAr.Trim();
        if (input.BodyEn is not null)
            page.BodyEn = input.BodyEn.Trim();
        page.UpdatedAt = DateTimeOffset.UtcNow;
        db.Audit.Add(new()
        {
            Actor = actor,
            Action = action,
            Detail = detail
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException
            || ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return RecordUpdateOutcome.Conflict;
        }
        return RecordUpdateOutcome.Saved;
    }
}
