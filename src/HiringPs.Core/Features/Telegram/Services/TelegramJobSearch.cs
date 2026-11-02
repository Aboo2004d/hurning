using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace HiringPs.Core;

/// <summary>Bounded job-card queries; account and audience permissions are checked on every request.</summary>
public sealed class TelegramJobSearch(HiringDb db, IConfiguration configuration)
{
    public static string Help(TelegramMemberSetting settings, bool en) =>
        (en ? "Search jobs (optional page after |):" : "البحث عن وظائف (يمكن إضافة رقم الصفحة بعد |):") + "\n"
        + (settings.CityEnabled ? "/jobs city " + (en ? "Hebron" : "الخليل") + "\n" : "")
        + (settings.OrganizationEnabled ? "/jobs organization " + (en ? "organization name" : "اسم الجهة") + "\n" : "")
        + (settings.CategoryEnabled ? "/jobs category " + (en ? "category name" : "اسم المجال") + "\n" : "");

    public async Task<TelegramJobAccess> AccessAsync(string userId, CancellationToken ct)
    {
        var member = await db.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var en = member?.Language == "en";
        var settings = await db.TelegramMemberSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new();
        var now = DateTimeOffset.UtcNow;
        string? error = !settings.SearchEnabled ? (en ? "Job search is currently disabled." : "البحث عن الوظائف متوقف حاليًا.")
            : member is not { Activated: true, Suspended: false, TelegramChatId: not null, TelegramUserId: not null }
                ? (en ? "Link and activate your account first." : "اربط حسابك وفعّله أولًا.")
            : settings.PaidOnly && !await db.MemberSubscriptions.AnyAsync(x => x.UserId == userId && !x.Cancelled
                && x.StartsAt <= now && x.EndsAt > now && x.PlanVersion.PriceIls > 0, ct)
                ? (en ? "Job search requires an active paid subscription." : "البحث يتطلب اشتراكًا مدفوعًا ساريًا.") : null;
        return new(member, settings, error);
    }

    public async Task<string> ReplyAsync(string userId, string input, CancellationToken ct) =>
        (await QueryAsync(userId, input, ct)).Text;

    public async Task<TelegramJobReply> QueryAsync(string userId, string input, CancellationToken ct)
    {
        var access = await AccessAsync(userId, ct);
        if (access.Error is not null) return new(access.Error, false);
        var member = access.Member!;
        var settings = access.Settings;
        var en = member.Language == "en";
        var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || input.Length > 1200) return new(Help(settings, en), false);
        var field = parts[0].ToLowerInvariant();
        var pageParts = parts[1].Split('|');
        var value = pageParts[0].Trim();
        var page = 1;
        if (value.Length is < 2 or > 1000 || pageParts.Length > 2
            || (pageParts.Length == 2 && (!int.TryParse(pageParts[1].Trim(), out page) || page is < 1 or > 100)))
            return new(Help(settings, en), false);
        return await SelectionAsync(userId, field, value, page, false, ct);
    }

    public async Task<TelegramJobReply> SelectionAsync(string userId, string field, string value, int page,
        bool exactOrganization, CancellationToken ct, bool includeNextCommand = true)
    {
        var access = await AccessAsync(userId, ct);
        if (access.Error is not null) return new(access.Error, false);
        var member = access.Member!;
        var settings = access.Settings;
        var en = member.Language == "en";
        if (page is < 1 or > 100 || value.Length is < 2 or > 1000) return new(Help(settings, en), false);
        var query = PublicOpportunityScope.Current(db.Opportunities.AsNoTracking(), TextRules.PalestineToday())
            .Where(x => x.Kind == OpportunityKind.Job);
        switch (field)
        {
            case "city" when settings.CityEnabled:
            case "مدينة" when settings.CityEnabled:
                var city = LocationSearch.Normalize(value);
                if (city.Length < 2) return new(Help(settings, en), false);
                query = query.Where(x => (" " + x.LocationSearchText + " ").Contains(" " + city + " "));
                break;
            case "organization" when settings.OrganizationEnabled:
            case "جهة" when settings.OrganizationEnabled:
                query = exactOrganization ? query.Where(x => x.Organization == value) : query.Where(x => x.Organization.Contains(value));
                break;
            case "category" when settings.CategoryEnabled:
            case "مجال" when settings.CategoryEnabled:
                var groups = await new CategoryGroupReadService(db).MapAsync(ct);
                var labels = await query.Where(x => x.Category != "").Select(x => x.Category).Distinct().ToArrayAsync(ct);
                var selected = labels.Where(x => groups.Matches(value, x) || (groups.GroupName(x) is { } group && CategoryRules.Key(group) == CategoryRules.Key(value))).ToArray();
                query = query.Where(x => selected.Contains(x.Category));
                break;
            default: return new(Help(settings, en), false);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 5).Take(6).Select(x => new { x.Id, x.Title, x.Organization, x.Locations }).ToListAsync(ct);
        if (rows.Count == 0) return new(en ? "No matching current jobs." : "لا توجد وظائف سارية مطابقة.", false);
        var origin = (configuration["Seo:PublicOrigin"] ?? "https://hiring.ps").TrimEnd('/') + "/" + member.Language;
        // Only publicly listed card fields are returned; details links retain the website's access rules.
        var text = (en ? "Page " : "الصفحة ") + page + "\n\n" + string.Join("\n\n", rows.Take(5).Select(x =>
            x.Title.Truncate(140) + "\n" + x.Organization.Truncate(90) + " · " + x.Locations.Truncate(90)
            + "\n" + origin + "/Opportunities/Details/" + x.Id));
        if (includeNextCommand && rows.Count > 5 && page < 100) text += "\n\n" + (en ? "Next: " : "التالي: ") + "/jobs " + field + " " + value + " | " + (page + 1);
        return new(text, rows.Count > 5 && page < 100);
    }
}

public sealed record TelegramJobAccess(MemberProfile? Member, TelegramMemberSetting Settings, string? Error);
public sealed record TelegramJobReply(string Text, bool HasMore);
