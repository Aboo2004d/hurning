using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads source item details and requests the existing explicit reprocessing path.</summary>
public sealed class SourceItemAdministrationService(HiringDb db)
{
    /// <summary>Projects unpublished queue rows; callers paginate before materialization.</summary>
    public IQueryable<SourceItemSummaryDto> Queue() => db.SourceItems.AsNoTracking()
        .Where(x => x.Status != ProcessingStatus.Published).OrderByDescending(x => x.Id)
        .Select(x => new SourceItemSummaryDto(x.Id, x.Source.Name, x.ExternalId, x.Status, x.Error));

    /// <summary>Loads only the fields needed by the authorized item detail screen.</summary>
    public Task<SourceItemDetailsDto?> ReadAsync(long id, CancellationToken ct) => db.SourceItems.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new SourceItemDetailsDto(x.Id, x.Source.Name, x.ExternalId, x.Status,
            x.Error, x.Url, x.Content, x.ExtractionJson, x.ExtractionPromptVersionId)).FirstOrDefaultAsync(ct);

    /// <summary>Resets retry metadata; PDF retries refetch the original instead of trusting an old transcription.</summary>
    public async Task<bool> RetryAsync(long id, string actor, CancellationToken ct)
    {
        var item = await db.SourceItems.Include(x => x.Source).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return false;
        var refreshPdf = PdfRetry.NeedsFreshContent(item);
        item.Status = !refreshPdf && item.Content.Length > 0 ? ProcessingStatus.AwaitingAi : ProcessingStatus.Discovered;
        if (refreshPdf)
            item.ExtractionJson = "";
        item.RetryAt = null;
        item.Attempts = 0;
        item.Error = string.Empty;
        db.Audit.Add(new()
        {
            Actor = actor,
            Action = "Item.Retry",
            Detail = $"Item {id}"
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
