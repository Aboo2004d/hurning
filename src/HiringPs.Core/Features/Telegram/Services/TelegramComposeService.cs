using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يجهز منشورات قنوات تليجرام ويجدولها يدويًا بنفس طابور التسليم المحمي من التكرار.</summary>
public sealed class TelegramComposeService(HiringDb db, TelegramChannelPublisher publisher,
    SocialTemplates templates, IConfiguration configuration)
{
    /// <summary>يقرأ النص والقناة المطابقة للنوع والقناة العامة وحالة التسليم السابقة.</summary>
    public async Task<TelegramComposeDto?> ReadAsync(long id, CancellationToken ct)
    {
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || !SocialPublishing.Eligible(item, TextRules.PalestineToday())) return null;
        SocialTemplateVersion? template = null;
        try { template = await templates.ResolveAsync(TelegramChannelTargets.Provider, item.Kind, ct); }
        catch (InvalidOperationException ex) when (ex.Message == "SocialTemplateMissing") { }
        var prepared = await publisher.BuildAsync(item, ct);
        var previous = await db.SocialPublications.AsNoTracking()
            .Where(x => x.OpportunityId == id && x.Provider == TelegramChannelTargets.Provider)
            .Select(x => new { x.PageId, x.Status }).ToListAsync(ct);
        var destinations = TelegramChannelTargets.ForOpportunity(item.Kind).Select(target =>
        {
            var channel = TelegramChannelTargets.Channel(configuration, target);
            var status = previous.FirstOrDefault(x => x.PageId == channel)?.Status;
            return new TelegramComposeDestinationDto(target, channel, publisher.Ready && channel.Length > 0 && status is null, status);
        }).ToArray();
        var body = template?.Body ?? (item.OriginalLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? TelegramChannelPublisher.DefaultEnglishBody : TelegramChannelPublisher.DefaultArabicBody);
        return new(id, item.Title, TelegramChannelPublisher.Shorten(SocialTemplateText.Render(body, item), 3500),
            prepared.Link, template?.Id, destinations);
    }

    /// <summary>يتحقق من القنوات والنص والقالب ويضيف جميع الوجهات في حفظ ذري واحد دون اتصال بتليجرام.</summary>
    public Task<TelegramComposeOutcome> QueueAsync(TelegramComposeCommandDto input, CancellationToken ct) =>
        QueueCoreAsync(input, true, ct);

    /// <summary>يعيد محاولة الحجز مرة واحدة عند سباق مع الطابور التلقائي، دون إعادة إرسال رسالة للمزود.</summary>
    private async Task<TelegramComposeOutcome> QueueCoreAsync(TelegramComposeCommandDto input, bool retryConflict, CancellationToken ct)
    {
        if (input.Id == Guid.Empty || string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 4096
            || input.Targets.Length is < 1 or > 2) return TelegramComposeOutcome.Invalid;
        var data = await ReadAsync(input.OpportunityId, ct);
        if (data is null) return TelegramComposeOutcome.Invalid;
        var item = await db.Opportunities.AsNoTracking().SingleAsync(x => x.Id == input.OpportunityId, ct);
        if (input.TemplateVersionId is { } suppliedVersion)
        {
            if (!await db.SocialTemplateVersions.AnyAsync(x => x.Id == suppliedVersion && x.Provider == TelegramChannelTargets.Provider
                && (x.Scope == "general" || x.Scope == item.Kind.ToString()), ct)) return TelegramComposeOutcome.Invalid;
        }
        else if (data.TemplateVersionId is not null) return TelegramComposeOutcome.Invalid;
        var targets = input.Targets.Distinct(StringComparer.Ordinal).ToArray();
        if (targets.Any(x => !data.Destinations.Any(d => d.Target == x && d.Channel.Length > 0)) || !publisher.Ready)
            return TelegramComposeOutcome.Invalid;
        var added = 0;
        foreach (var channel in data.Destinations.Where(x => targets.Contains(x.Target)).Select(x => x.Channel).Distinct())
        {
            if (await db.SocialPublications.AnyAsync(x => x.OpportunityId == item.Id
                && x.Provider == TelegramChannelTargets.Provider && x.PageId == channel, ct)) continue;
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(input.Id.ToString("N") + ":" + channel)).AsSpan(0, 16));
            db.SocialPublications.Add(new SocialPublication
            {
                Id = id, OpportunityId = item.Id, Provider = TelegramChannelTargets.Provider,
                PageId = channel, Text = TelegramChannelPublisher.Compose(input.Text, item.Title, data.Link),
                TemplateVersionId = input.TemplateVersionId, ManualCompose = true, Actor = input.Actor.Truncate(256),
            });
            added++;
        }
        if (added == 0) return TelegramComposeOutcome.AlreadyQueued;
        db.Audit.Add(new() { Actor = input.Actor.Truncate(200), Action = "TelegramChannel.ManualQueued",
            Detail = $"Opportunity {item.Id}; destinations {added}" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return retryConflict ? await QueueCoreAsync(input, false, ct) : TelegramComposeOutcome.Invalid;
        }
        return TelegramComposeOutcome.Queued;
    }
}
