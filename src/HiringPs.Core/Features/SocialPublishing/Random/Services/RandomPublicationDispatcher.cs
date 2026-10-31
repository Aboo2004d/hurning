using Microsoft.EntityFrameworkCore;
namespace HiringPs.Core;

/// <summary>يربط الجدولة بالقالب الفعال وخدمة النشر الحالية دون إنشاء بروتوكول إرسال مكرر.</summary>
public sealed class RandomPublicationDispatcher(HiringDb db, SocialComposeReadService compose,
    SocialPublicationService publications) : IRandomPublicationDispatcher
{
    /// <summary>يجهز القالب ويحفظ نتيجة المحاولة المشتركة لكل وجهة.</summary>
    public async Task<RandomPublicationDeliveryDto> SendAsync(RandomPublicationRun run, CancellationToken ct)
    {
        var result = await compose.ReadAsync(run.OpportunityId!.Value, run.Provider, ct);
        if (result.Status != SocialComposeReadStatus.Ready || result.Data is not { } data)
            return new(RandomPublicationState.Skipped, "Ineligible");
        var sent = await publications.PublishAsync(new(run.Id, run.OpportunityId.Value, run.Provider,
            data.TemplateVersionId, data.Text, false, "Random publisher", new(run.PageId)), ct);
        var attempt = await db.SocialPublications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.Id, ct);
        return attempt?.Status switch
        {
            "Published" => new(RandomPublicationState.Published, "Published"),
            "Failed" => new(RandomPublicationState.Failed, "PreparationFailed"),
            "Unknown" or "Pending" => new(RandomPublicationState.Unknown, "Unconfirmed"),
            _ => new(RandomPublicationState.Skipped, sent.Notice ?? "Ineligible"),
        };
    }
}
