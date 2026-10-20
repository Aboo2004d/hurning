using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءات لوحة التوزيع؛ تعيد بيانات مختصرة ولا تتصل بتليجرام أو محرك البحث.</summary>
public sealed class DistributionReadService(HiringDb db)
{
    /// <summary>يعيد حالات تفعيل وجهات القنوات.</summary>
    public Task<List<TelegramChannelSettingSummaryDto>> SettingsAsync(CancellationToken ct) =>
        db.TelegramChannelSettings.AsNoTracking()
            .Select(x => new TelegramChannelSettingSummaryDto(x.Target, x.Enabled, x.EnabledAt)).ToListAsync(ct);

    /// <summary>يسقط سجل تليجرام بالحالة المطلوبة قبل ترقيم الصفحة وبنفس ترتيب السجل الأصلي.</summary>
    public IQueryable<TelegramChannelPostSummaryDto> Posts(string? state)
    {
        var posts = db.SocialPublications.AsNoTracking().Where(x => x.Provider == TelegramChannelTargets.Provider);
        if (state is "Pending" or "Published" or "Failed" or "Unknown" or "Skipped")
            posts = posts.Where(x => x.Status == state);
        return posts.OrderByDescending(x => x.CreatedAt).Select(x => new TelegramChannelPostSummaryDto
        {
            Id = x.Id, OpportunityId = x.OpportunityId, Opportunity = new SocialOpportunitySummaryDto(x.Opportunity.Title),
            PageId = x.PageId, Status = x.Status, Error = x.Error, Attempts = x.Attempts,
            CreatedAt = x.CreatedAt, NextAttemptAt = x.NextAttemptAt, Url = x.Url,
        });
    }

    /// <summary>يحسب حالة طابور الفهرسة وآخر عشر نتائج دون إرسال أي إخطار.</summary>
    public async Task<IndexNowStatusDto> IndexNowAsync(CancellationToken ct) => new(
        await db.IndexNowEntries.CountAsync(x => x.ProcessedAt == null || x.ProcessedAt < x.QueuedAt, ct),
        await db.IndexNowEntries.CountAsync(x => x.Attempts > 0, ct),
        await db.IndexNowEntries.MaxAsync(x => x.SubmittedAt, ct),
        await db.IndexNowEntries.AsNoTracking().OrderByDescending(x => x.SubmittedAt ?? x.QueuedAt).Take(10)
            .Select(x => new IndexNowEntrySummaryDto(x.OpportunityId, x.SubmittedAt, x.LastStatus, x.LastError, x.Attempts)).ToListAsync(ct));
}
