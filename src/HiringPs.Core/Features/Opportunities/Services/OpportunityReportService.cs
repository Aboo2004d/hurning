using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يسجل بلاغ زائر دون تغيير حالة الفرصة أو نشرها تلقائيًا.</summary>
public sealed class OpportunityReportService(HiringDb db)
{
    /// <summary>يفحص أسباب البلاغ وحجم التفاصيل المسموحين في النموذج القائم.</summary>
    public static bool Valid(string reason, string? details) =>
        reason is "expired" or "duplicate" or "incorrect" or "broken" or "other"
        && (details?.Length ?? 0) <= 1000;

    /// <summary>يحفظ بلاغًا لفرصة منشورة أو مؤرشفة ويعيد عنوانها لبناء رابط العودة؛ null تعني غيابها.</summary>
    public async Task<string?> SubmitAsync(long id, string reason, string? details, CancellationToken ct)
    {
        if (!Valid(reason, details))
            throw new ArgumentException("Invalid opportunity report.");
        var title = await db.Opportunities
            .Where(x => x.Id == id && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived))
            .Select(x => x.Title).FirstOrDefaultAsync(ct);
        if (title is null)
            return null;
        db.Reports.Add(new()
        {
            OpportunityId = id,
            Reason = reason,
            Details = details ?? ""
        });
        await db.SaveChangesAsync(ct);
        return title;
    }
}
