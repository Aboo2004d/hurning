// النشر التلقائي في قناة تليجرام العامة: الإعدادات، العميل، وطابور التسليم فوق سجل النشر الاجتماعي.
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون TelegramChannelPublisher ضمن مجال Telegram؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed class TelegramChannelPublisher(
    HiringDb db,
    TelegramChannelClient client,
    SocialTemplates templates,
    IConfiguration configuration,
    ILogger<TelegramChannelPublisher>? logger = null
)
{
    public const string Provider = TelegramChannelTargets.Provider;
    public const int MaxAttempts = 8;
    public const int MessageLimit = 4096;
    private const int SummaryLimit = 600;

    public const string DefaultArabicBody =
        "📢 {{kind}}\n\n💼 {{title}}\n🏢 {{organization}}\n📍 {{location}}\n📅 آخر موعد للتقديم: {{deadline}}\n\n{{summary}}";
    public const string DefaultEnglishBody =
        "📢 {{kind}}\n\n💼 {{title}}\n🏢 {{organization}}\n📍 {{location}}\n📅 Apply by: {{deadline}}\n\n{{summary}}";

    public bool Ready => client.Ready;

    /// <summary>Queue newly published opportunities for every enabled target with a configured channel.</summary>
    public async Task<int> EnqueueAsync(CancellationToken ct)
    {
        if (!client.Ready)
            return 0;
        var settings = await db
            .TelegramChannelSettings.AsNoTracking()
            .Where(x => x.Enabled && x.EnabledAt != null)
            .ToListAsync(ct);
        var today = TextRules.PalestineToday();
        var added = 0;
        foreach (var setting in settings)
        {
            var channel = TelegramChannelTargets.Channel(configuration, setting.Target);
            if (channel.Length == 0)
                continue;
            var kinds = TelegramChannelTargets.Kinds(setting.Target);
            var since = setting.EnabledAt!.Value;
            var ids = await db
                .Opportunities.AsNoTracking()
                .Where(x =>
                    x.Status == PublicationStatus.Published
                    && kinds.Contains(x.Kind)
                    && (x.Deadline == null || x.Deadline >= today)
                    && x.ClosureEvidence == ""
                    && db.OpportunityPublications.Any(p => p.OpportunityId == x.Id && p.PublishedAt >= since)
                    && !db.SocialPublications.Any(s =>
                        s.OpportunityId == x.Id && s.Provider == Provider && s.PageId == channel
                    )
                )
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Take(20)
                .ToListAsync(ct);
            foreach (var id in ids)
                db.SocialPublications.Add(
                    new SocialPublication
                    {
                        Id = Guid.NewGuid(),
                        OpportunityId = id,
                        Provider = Provider,
                        PageId = channel,
                        Status = "Pending",
                        Actor = "Worker:" + setting.Target,
                    }
                );
            try
            {
                added += await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The filtered unique index already holds this opportunity/channel pair.
                db.ChangeTracker.Clear();
            }
        }
        return added;
    }

    /// <summary>Send up to <paramref name="max"/> due posts. Returns the number of provider calls made.</summary>
    public async Task<int> DeliverAsync(int max, TimeSpan spacing, CancellationToken ct)
    {
        if (!client.Ready)
            return 0;
        var now = DateTimeOffset.UtcNow;
        var ids = await db
            .SocialPublications.AsNoTracking()
            .Where(x =>
                x.Provider == Provider
                && x.Status == "Pending"
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
            )
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(max)
            .ToListAsync(ct);
        var calls = 0;
        foreach (var id in ids)
        {
            // Claim with a lease so a second worker cannot send the same row concurrently.
            if (
                await db
                    .SocialPublications.Where(x =>
                        x.Id == id
                        && x.Status == "Pending"
                        && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
                    )
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.NextAttemptAt, now.AddMinutes(10)), ct)
                == 0
            )
                continue;
            var attempt = await db.SocialPublications.SingleAsync(x => x.Id == id, ct);
            await db.Entry(attempt).ReloadAsync(ct);
            var item = await db.Opportunities.AsNoTracking().SingleAsync(x => x.Id == attempt.OpportunityId, ct);
            var today = TextRules.PalestineToday();
            if (!SocialPublishing.Eligible(item, today))
            {
                Finish(attempt, "Skipped", "NotEligible");
                await db.SaveChangesAsync(CancellationToken.None);
                continue;
            }
            var manualChannelConfigured = attempt.ManualCompose && TelegramChannelTargets.ForOpportunity(item.Kind)
                .Any(target => TelegramChannelTargets.Channel(configuration, target) == attempt.PageId);
            if (attempt.ManualCompose ? !manualChannelConfigured : !await ChannelStillEnabledAsync(attempt.PageId, item.Kind, ct))
            {
                Finish(attempt, "Skipped", "TelegramChannelDisabled");
                await db.SaveChangesAsync(CancellationToken.None);
                continue;
            }
            (string Html, long? TemplateVersionId, string Link) message;
            try
            {
                message = await BuildAsync(item, ct);
                if (attempt.ManualCompose)
                    message = (attempt.Text, attempt.TemplateVersionId, message.Link);
            }
            catch (InvalidOperationException ex) when (ex.Message == "PublicOriginRequired")
            {
                attempt.Error = ex.Message;
                attempt.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(30);
                await db.SaveChangesAsync(CancellationToken.None);
                continue;
            }
            attempt.Text = message.Html;
            attempt.TemplateVersionId = message.TemplateVersionId;
            attempt.Attempts++;
            if (calls > 0 && spacing > TimeSpan.Zero)
                await Task.Delay(spacing, ct);
            calls++;
            var result = await client.SendAsync(attempt.PageId, message.Html, message.Link, ct);
            Apply(attempt, result, DateTimeOffset.UtcNow);
            logger?.LogInformation(
                "TelegramChannel Opportunity {OpportunityId} Channel {Channel} Status {Status} Code {Code}",
                attempt.OpportunityId,
                attempt.PageId,
                attempt.Status,
                result.Code
            );
            // Record the provider result even if the host is stopping.
            await db.SaveChangesAsync(CancellationToken.None);
            if (result.Outcome == TelegramChannelOutcome.RetryLater && result.Code == "TelegramRateLimited")
                break;
        }
        return calls;
    }

    /// <summary>ينفذ Finish وفق قواعد TelegramChannelPublisher الحالية دون تغيير عقد المستدعين.</summary>
    private static void Finish(SocialPublication attempt, string status, string error)
    {
        attempt.Status = status;
        attempt.Error = error;
        attempt.NextAttemptAt = null;
    }

    /// <summary>يحول نتيجة المزود إلى حالة تسليم وموعد إعادة محاولة وفق السياسة الحالية.</summary>
    public static void Apply(SocialPublication attempt, TelegramChannelResult result, DateTimeOffset now)
    {
        switch (result.Outcome)
        {
            case TelegramChannelOutcome.Sent:
                attempt.RemoteId = result.RemoteId;
                attempt.Url = result.Url;
                Finish(attempt, "Published", "");
                break;
            case TelegramChannelOutcome.RetryLater when result.Code == "TelegramRateLimited":
                // Flood control is not a failure of this post; do not consume an attempt.
                attempt.Attempts = Math.Max(0, attempt.Attempts - 1);
                attempt.Status = "Pending";
                attempt.Error = result.Code;
                attempt.NextAttemptAt = now.AddSeconds((result.RetryAfterSeconds ?? 60) + 1);
                break;
            case TelegramChannelOutcome.RetryLater when attempt.Attempts < MaxAttempts:
                attempt.Status = "Pending";
                attempt.Error = result.Code;
                attempt.NextAttemptAt = now.AddMinutes(
                    Math.Max(
                        (result.RetryAfterSeconds ?? 60) / 60.0,
                        Math.Min(360, Math.Pow(2, attempt.Attempts))
                    )
                );
                break;
            case TelegramChannelOutcome.Ambiguous:
                // Telegram may have accepted the message: never resend automatically.
                Finish(attempt, "Unknown", result.Code);
                break;
            default:
                Finish(attempt, "Failed", result.Code);
                break;
        }
    }

    /// <summary>ينفذ ChannelStillEnabledAsync وفق قواعد TelegramChannelPublisher الحالية دون تغيير عقد المستدعين.</summary>
    private async Task<bool> ChannelStillEnabledAsync(string channel, OpportunityKind kind, CancellationToken ct)
    {
        var enabled = await db
            .TelegramChannelSettings.AsNoTracking()
            .Where(x => x.Enabled)
            .Select(x => x.Target)
            .ToListAsync(ct);
        return enabled.Any(target =>
            TelegramChannelTargets.Kinds(target).Contains(kind)
            && TelegramChannelTargets.Channel(configuration, target) == channel
        );
    }

    /// <summary>يختار نسخة قالب القناة ويبني النص والرابط من الفرصة المعتمدة.</summary>
    public async Task<(string Html, long? TemplateVersionId, string Link)> BuildAsync(
        Opportunity item,
        CancellationToken ct
    )
    {
        var english = item.OriginalLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var language = english ? "en" : "ar";
        var origin = PublicUrls.Origin(configuration);
        // Gated kinds link to the public listing instead of a details page visitors cannot open.
        var publicDetails =
            await db
                .OpportunityAccessRules.AsNoTracking()
                .Where(x => x.Kind == item.Kind)
                .Select(x => (bool?)x.PublicDetails)
                .SingleOrDefaultAsync(ct) ?? true;
        var link = PublicUrls.WithSource(
            publicDetails
                ? PublicUrls.Details(origin, item.Id, language)
                : PublicUrls.Listing(origin, language, item.Kind),
            "telegram"
        );
        string body;
        long? versionId = null;
        try
        {
            var template = await templates.ResolveAsync(Provider, item.Kind, ct);
            body = template.Body;
            versionId = template.Id;
        }
        catch (InvalidOperationException)
        {
            body = english ? DefaultEnglishBody : DefaultArabicBody;
        }
        var shortItem = new Opportunity
        {
            Kind = item.Kind,
            Title = item.Title,
            Organization = item.Organization,
            Locations = item.Locations,
            Deadline = item.Deadline,
            OriginalLanguage = item.OriginalLanguage,
            Summary = Shorten(item.Summary, SummaryLimit),
        };
        return (Compose(SocialTemplateText.Render(body, shortItem), item.Title, link), versionId, link);
    }

    /// <summary>Escape for Telegram HTML mode, bold the title, append the link, and stay within 4096 characters.</summary>
    public static string Compose(string plain, string title, string link)
    {
        var footer = "\n\n🔗 " + link;
        var budget = MessageLimit - footer.Length;
        if (plain.Length > budget)
            plain = Shorten(plain, budget);
        var html = Escape(plain);
        var escapedTitle = Escape(title.Trim());
        var at = escapedTitle.Length > 0 ? html.IndexOf(escapedTitle, StringComparison.Ordinal) : -1;
        if (at >= 0)
            html = html[..at] + "<b>" + escapedTitle + "</b>" + html[(at + escapedTitle.Length)..];
        return html + "\n\n🔗 " + Escape(link);
    }

    /// <summary>ينفذ Escape وفق قواعد TelegramChannelPublisher الحالية دون تغيير عقد المستدعين.</summary>
    public static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>ينفذ Shorten وفق قواعد TelegramChannelPublisher الحالية دون تغيير عقد المستدعين.</summary>
    public static string Shorten(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;
        var cut = Math.Max(0, max - 1);
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
            cut--;
        var space = text.LastIndexOfAny([' ', '\n'], Math.Max(0, cut - 1));
        if (space > cut - 80 && space > 0)
            cut = space;
        return text[..cut].TrimEnd() + "…";
    }

    /// <summary>Send a fixed test message to a configured target; records nothing in the publication queue.</summary>
    public async Task<TelegramChannelResult> TestAsync(string target, CancellationToken ct)
    {
        var channel = TelegramChannelTargets.Channel(configuration, target);
        if (channel.Length == 0)
            return new(TelegramChannelOutcome.Rejected, "TelegramChannelNotConfigured");
        var origin = PublicUrls.Origin(configuration);
        var link = PublicUrls.WithSource(origin + "/ar", "telegram");
        var text =
            "✅ "
            + Escape("رسالة تجريبية من لوحة الإدارة للتأكد من صلاحية البوت في هذه القناة.")
            + "\n"
            + Escape("Test message from the admin panel to verify the bot can post here.")
            + "\n<i>"
            + Escape(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TextRules.PalestineTimeZone).ToString("yyyy-MM-dd HH:mm"))
            + "</i>\n\n🔗 "
            + Escape(link);
        return await client.SendAsync(channel, text, link, ct);
    }
}
