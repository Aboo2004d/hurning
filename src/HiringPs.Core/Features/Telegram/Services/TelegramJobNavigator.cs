using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>Stateless search buttons; callback keys identify current public catalog values, never grant access.</summary>
public sealed class TelegramJobNavigator(HiringDb db, TelegramClient telegram, IConfiguration configuration)
{
    private const int OptionsPerPage = 8;
    public static string Key(string value) => TextRules.Hash(value)[..16];
    private static bool Enabled(TelegramMemberSetting settings, string field) => field switch
    {
        "category" => settings.CategoryEnabled,
        "city" => settings.CityEnabled,
        "organization" => settings.OrganizationEnabled,
        _ => false,
    };
    private static string Label(string field, bool en) => field switch
    {
        "category" => en ? "By category" : "حسب المجال",
        "city" => en ? "By location" : "حسب المكان",
        _ => en ? "By organization" : "حسب الجهة",
    };

    public async Task OpenAsync(long from, long chat, string data, CancellationToken ct)
    {
        if (from != chat || data.Length > 64) return;
        var member = await db.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.TelegramUserId == from && x.TelegramChatId == chat, ct);
        if (member is null)
        {
            await telegram.SendAsync(chat, "اربط حسابك وفعّله من الموقع أولًا. / Link and activate your account first.", ct);
            return;
        }
        var search = new TelegramJobSearch(db, configuration);
        var access = await search.AccessAsync(member.UserId, ct);
        if (access.Error is not null) { await telegram.SendAsync(chat, access.Error, ct); return; }
        var settings = access.Settings;
        var en = member.Language == "en";
        var back = en ? "Back to search" : "العودة لخيارات البحث";
        if (data == "jobs:m")
        {
            var menu = new[] { "category", "city", "organization" }.Where(x => Enabled(settings, x))
                .Select(x => (Label(x, en), "jobs:c:" + x + ":1")).ToArray();
            await telegram.SendAsync(chat, en ? "How would you like to search for jobs?" : "كيف تريد البحث عن الوظائف؟", ct, callbacks: menu);
            return;
        }
        var parts = data.Split(':');
        if (parts.Length < 4 || parts[0] != "jobs" || !Enabled(settings, parts[2]))
        { await OpenAsync(from, chat, "jobs:m", ct); return; }
        var field = parts[2];
        var options = await OptionsAsync(field, en, ct);
        if (parts[1] == "c" && parts.Length == 4 && int.TryParse(parts[3], out var optionPage) && optionPage is >= 1 and <= 10000)
        {
            var pageCount = Math.Max(1, (options.Count + OptionsPerPage - 1) / OptionsPerPage);
            optionPage = Math.Min(optionPage, pageCount);
            var buttons = options.Skip((optionPage - 1) * OptionsPerPage).Take(OptionsPerPage)
                .Select(x => (x.Label.Truncate(60), "jobs:p:" + field + ":" + Key(x.Value) + ":1")).ToList();
            if (optionPage > 1) buttons.Add((en ? "Previous options" : "الخيارات السابقة", "jobs:c:" + field + ":" + (optionPage - 1)));
            if (optionPage < pageCount && optionPage < 10000) buttons.Add((en ? "More options" : "خيارات أخرى", "jobs:c:" + field + ":" + (optionPage + 1)));
            buttons.Add((back, "jobs:m"));
            await telegram.SendAsync(chat, options.Count == 0
                ? (en ? "No options with current jobs." : "لا توجد خيارات لوظائف سارية حاليًا.")
                : Label(field, en) + " — " + (en ? "Choose an option" : "اختر من القائمة") + " (" + optionPage + "/" + pageCount + ")",
                ct, callbacks: buttons.ToArray());
            return;
        }
        if (parts[1] == "p" && parts.Length == 5 && int.TryParse(parts[4], out var resultPage) && resultPage is >= 1 and <= 100)
        {
            var selected = options.Where(x => Key(x.Value) == parts[3]).ToArray();
            // A stale, forged or ambiguous key cannot be used as a source value or a query fragment.
            if (selected.Length != 1) { await OpenAsync(from, chat, "jobs:c:" + field + ":1", ct); return; }
            var result = await search.SelectionAsync(member.UserId, field, selected[0].Value, resultPage, true, ct, includeNextCommand: false);
            var buttons = new List<(string Text, string Data)>();
            if (resultPage > 1) buttons.Add((en ? "Previous jobs" : "الوظائف السابقة", "jobs:p:" + field + ":" + parts[3] + ":" + (resultPage - 1)));
            if (result.HasMore) buttons.Add((en ? "More jobs" : "وظائف أخرى", "jobs:p:" + field + ":" + parts[3] + ":" + (resultPage + 1)));
            buttons.Add((en ? "Change selection" : "اختيار آخر", "jobs:c:" + field + ":1"));
            buttons.Add((back, "jobs:m"));
            await telegram.SendAsync(chat, selected[0].Label.Truncate(120) + "\n\n" + result.Text, ct, callbacks: buttons.ToArray());
            return;
        }
        await OpenAsync(from, chat, "jobs:m", ct);
    }

    public async Task<List<TelegramJobOption>> OptionsAsync(string field, bool en, CancellationToken ct)
    {
        var query = PublicOpportunityScope.Current(db.Opportunities.AsNoTracking(), TextRules.PalestineToday()).Where(x => x.Kind == OpportunityKind.Job);
        IEnumerable<TelegramJobOption> options;
        if (field == "organization")
            options = (await query.Where(x => x.Organization != "").Select(x => x.Organization).Distinct().ToArrayAsync(ct)).Select(x => new TelegramJobOption(x, x));
        else if (field == "category")
        {
            var groups = await new CategoryGroupReadService(db).MapAsync(ct);
            options = (await query.Where(x => x.Category != "").Select(x => x.Category).Distinct().ToArrayAsync(ct))
                .DistinctBy(groups.Identity).Select(x => groups.GroupName(x) ?? x).Select(x => new TelegramJobOption(x, x));
        }
        else if (field == "city")
        {
            var locations = await query.Where(x => x.Locations != "").Select(x => x.Locations).Distinct().ToArrayAsync(ct);
            var choices = new List<TelegramJobOption>();
            foreach (var location in locations)
            {
                foreach (var part in LocationCatalog.Split(location))
                {
                    var indexed = " " + LocationSearch.Normalize(part) + " ";
                    var groups = LocationSearch.Groups.Where(g => g.Any(alias => indexed.Contains(" " + LocationSearch.Normalize(alias) + " ", StringComparison.Ordinal))).ToArray();
                    if (groups.Length == 0) choices.Add(new(part, part));
                    else choices.AddRange(groups.Select(g => new TelegramJobOption(en ? g[1] : g[0], g[0])));
                }
            }
            options = choices.DistinctBy(x => LocationSearch.Normalize(x.Value));
        }
        else return [];
        return options.Where(x => x.Value.Length is >= 2 and <= 1000).DistinctBy(x => x.Value)
            .OrderBy(x => x.Label, StringComparer.Ordinal).ToList();
    }
}

public sealed record TelegramJobOption(string Label, string Value);
