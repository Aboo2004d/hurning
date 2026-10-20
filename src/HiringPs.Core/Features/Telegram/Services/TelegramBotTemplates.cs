using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قوالب تنبيهات الفرص في البوت؛ مستقلة عن قوالب القنوات ولا تغير رسائل الدفع أو أوامر الحساب.</summary>
public sealed class TelegramBotTemplates(HiringDb db)
{
    public const string Provider = "TelegramBot";
    public const string DefaultBody = "{{title}}";
    public const int MessageLimit = 4000;

    /// <summary>يعتمد قالب النوع ثم العام، ويبقي صيغة العنوان السابقة إن لم يعتمد المسؤول قالبًا بعد.</summary>
    public async Task<string> RenderAsync(Opportunity item, string link, string prefix, CancellationToken ct)
    {
        var scope = item.Kind.ToString();
        var body = await db.SocialTemplateVersions.AsNoTracking()
            .Where(x => x.Provider == Provider && x.Active && (x.Scope == scope || x.Scope == "general"))
            .OrderByDescending(x => x.Scope == scope).Select(x => x.Body).FirstOrDefaultAsync(ct)
            ?? DefaultBody;
        return Compose(body, item, link, prefix);
    }

    /// <summary>يولد نصًا عاديًا محدود الطول مع رابط إلزامي وتذكير الإغلاق إن وجد؛ لا يفسر HTML.</summary>
    public static string Compose(string body, Opportunity item, string link, string prefix = "")
    {
        var footer = "\n" + link;
        var text = prefix + SocialTemplateText.Render(body, item);
        var budget = MessageLimit - footer.Length;
        if (budget < 1)
            throw new InvalidOperationException("TelegramLinkTooLong");
        return TelegramChannelPublisher.Shorten(text, budget) + footer;
    }
}
