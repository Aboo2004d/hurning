using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يجهز قراءة شاشة النشر ويمنع خروج كيانات الربط ورموز الوصول إلى طبقة العرض.</summary>
public sealed class SocialComposeReadService(HiringDb db, SocialPublishing publishing, SocialTemplates templates)
{
    /// <summary>يتحقق من الفرصة ويختار القالب ثم يسقط الوجهات والسجل إلى حقول العرض فقط.</summary>
    public async Task<SocialComposeReadResultDto> ReadAsync(long id, string provider, CancellationToken ct)
    {
        if (!SocialPublishing.IsProvider(provider)) return new(SocialComposeReadStatus.InvalidRequest);
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return new(SocialComposeReadStatus.NotFound);
        if (!SocialPublishing.Eligible(item, TextRules.PalestineToday())) return new(SocialComposeReadStatus.InvalidRequest);
        var family = SocialPublishing.IsLinkedIn(provider) ? new[] { "LinkedIn", "LinkedInPersonal" } : new[] { provider };
        var history = await db.SocialPublications.AsNoTracking()
            .Where(x => x.OpportunityId == id && family.Contains(x.Provider))
            .OrderByDescending(x => x.CreatedAt).Take(40)
            .Select(x => new SocialPublicationSummaryDto(x.Provider, x.CreatedAt, x.Status, x.Url)).ToListAsync(ct);
        var template = await templates.ResolveAsync(provider, item.Kind, ct);
        var destinations = await db.SocialConnections.AsNoTracking()
            .Where(x => family.Contains(x.Provider))
            .Select(x => new SocialDestinationSummaryDto(x.Provider, x.PageName, x.Enabled, x.ExpiresAt)).ToListAsync(ct);
        var previouslyPublished = await db.SocialPublications.AnyAsync(x =>
            x.OpportunityId == id && family.Contains(x.Provider) && x.Status == "Published", ct);
        return new(SocialComposeReadStatus.Ready, new SocialComposeDataDto(id, provider,
            new SocialOpportunitySummaryDto(item.Title), SocialTemplateText.Render(template.Body, item),
            template.Id, template.Number, publishing.OpportunityUrl(id, item.OriginalLanguage),
            history, destinations, previouslyPublished));
    }
}
