using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace HiringPs.Core;

/// <summary>Private member announcements: explicit admin queueing, plain text variables, no HTTP sends.</summary>
public sealed partial class TelegramMemberMessaging(HiringDb db, IConfiguration configuration)
{
    public static bool ValidTemplate(string? body) => !string.IsNullOrWhiteSpace(body)
        && body.Length <= 3500
        && !body.Replace("{{name}}", "", StringComparison.Ordinal).Replace("{{site}}", "", StringComparison.Ordinal).Contains("{{", StringComparison.Ordinal)
        && !body.Replace("{{name}}", "", StringComparison.Ordinal).Replace("{{site}}", "", StringComparison.Ordinal).Contains("}}", StringComparison.Ordinal);
    [GeneratedRegex(@"\{\{(name|site)\}\}")] private static partial Regex Variables();
    public string Render(string body, MemberProfile member) => Variables().Replace(body, match => match.Value == "{{name}}"
        ? member.Name : (configuration["Seo:PublicOrigin"] ?? "https://hiring.ps").TrimEnd('/') + "/" + member.Language);
    public bool Fits(string body) => ValidTemplate(body) && Render(body, new MemberProfile { Name = new string('x', 160), Language = "en" }).Length <= 4000;

    public IQueryable<MemberProfile> Audience(bool paidOnly, string? userId, DateTimeOffset at) =>
        db.MemberProfiles.Where(x => x.Activated && !x.Suspended && x.TelegramChatId != null
            && x.TelegramUserId != null && x.NotificationsEnabled && x.CreatedAt <= at
            && (userId == null || x.UserId == userId)
            && (!paidOnly || db.MemberSubscriptions.Any(s => s.UserId == x.UserId && !s.Cancelled
                && s.StartsAt <= at && s.EndsAt > at && s.PlanVersion.PriceIls > 0)));

    public async Task<bool> QueueAsync(Guid id, string body, bool paidOnly, string? userId, string actor, CancellationToken ct)
    {
        if (id == Guid.Empty || !Fits(body)) return false;
        if (await db.TelegramMemberBroadcasts.AnyAsync(x => x.Id == id, ct)) return true;
        var now = DateTimeOffset.UtcNow;
        if (!await Audience(paidOnly, userId, now).AnyAsync(ct)) return false;
        db.TelegramMemberBroadcasts.Add(new() { Id = id, Body = body, PaidOnly = paidOnly, UserId = userId,
            Actor = actor.Truncate(200), CreatedAt = now });
        new MembershipService(db).Audit(actor, "Telegram.BroadcastQueued", $"Broadcast {id}; PaidOnly {paidOnly}; Member {userId ?? "all"}");
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ExpandAsync(CancellationToken ct)
    {
        var broadcast = await db.TelegramMemberBroadcasts.Where(x => !x.Completed).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (broadcast is null) return;
        var members = await Audience(broadcast.PaidOnly, broadcast.UserId, broadcast.CreatedAt)
            .Where(x => broadcast.AfterUserId == null || string.Compare(x.UserId, broadcast.AfterUserId) > 0)
            .OrderBy(x => x.UserId).Take(100).ToListAsync(ct);
        var key = "announcement:" + broadcast.Id;
        var memberIds = members.Select(x => x.UserId).ToArray();
        var existing = await db.MemberMessages.Where(x => x.EventKey == key && memberIds.Contains(x.UserId)).Select(x => x.UserId).ToListAsync(ct);
        foreach (var member in members.Where(x => !existing.Contains(x.UserId)))
            db.MemberMessages.Add(new() { UserId = member.UserId, EventKey = key, Kind = MemberMessageKind.Announcement,
                Text = Render(broadcast.Body, member), EventAt = broadcast.CreatedAt });
        broadcast.AfterUserId = members.LastOrDefault()?.UserId ?? broadcast.AfterUserId;
        broadcast.Completed = members.Count < 100;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> CanDeliverAsync(MemberMessage message, CancellationToken ct)
    {
        if (!message.EventKey.StartsWith("announcement:", StringComparison.Ordinal)
            || !Guid.TryParse(message.EventKey["announcement:".Length..], out var id)) return false;
        var broadcast = await db.TelegramMemberBroadcasts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return broadcast is not null && await Audience(broadcast.PaidOnly, message.UserId, DateTimeOffset.UtcNow).AnyAsync(ct);
    }
}
