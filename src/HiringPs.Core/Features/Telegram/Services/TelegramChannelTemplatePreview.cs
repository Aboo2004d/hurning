using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>معاينة نص قناة تليجرام بنفس قواعد الرابط والملخص وحد الطول المستخدمة في التسليم.</summary>
public sealed class TelegramChannelTemplatePreview(HiringDb db, SocialPublishing publishing)
{
    /// <summary>يختار رابط التفاصيل العامة أو قائمة النوع دون إرسال رسالة أو إنشاء محاولة نشر.</summary>
    public async Task<string> RenderAsync(string body, Opportunity item, CancellationToken ct)
    {
        var link = publishing.OpportunityUrl(item.Id, item.OriginalLanguage);
        var publicDetails = await db.OpportunityAccessRules.Where(x => x.Kind == item.Kind)
            .Select(x => (bool?)x.PublicDetails).SingleOrDefaultAsync(ct) ?? true;
        if (!publicDetails)
        {
            var origin = new Uri(link).GetLeftPart(UriPartial.Authority);
            link = PublicUrls.Listing(origin, item.OriginalLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar", item.Kind);
        }
        var plain = SocialTemplateText.Render(body, new Opportunity
        {
            Kind = item.Kind,
            Title = item.Title,
            Organization = item.Organization,
            Locations = item.Locations,
            Deadline = item.Deadline,
            OriginalLanguage = item.OriginalLanguage,
            Summary = TelegramChannelPublisher.Shorten(item.Summary, 600),
        });
        link = PublicUrls.WithSource(link, "telegram");
        var footer = "\n\n🔗 " + link;
        return TelegramChannelPublisher.Shorten(plain, TelegramChannelPublisher.MessageLimit - footer.Length) + footer;
    }
}
