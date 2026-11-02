using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>Combined filter menus; compact callback snapshots preserve choices while each step rechecks current access.</summary>
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

    private static readonly string[] Fields = ["city", "organization", "category"];
    private static string Code(string field) => field switch { "city" => "l", "organization" => "o", _ => "g" };
    private static string? Field(string code) => code switch { "l" => "city", "o" => "organization", "g" => "category", _ => null };
    // Three 64-bit keys fit together with action, page and choice under Telegram's 64-byte limit.
    private static string FilterKey(string value) => Convert.ToBase64String(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))[..8])
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string State(TelegramJobFilters filters) => string.Join('.', Fields.Select(field =>
        filters.Value(field) is { } value ? FilterKey(value) : "-"));

    private async Task<(TelegramJobFilters Filters, bool Changed)> ResolveAsync(string state, TelegramMemberSetting settings, bool en, CancellationToken ct)
    {
        var keys = state.Split('.');
        if (keys.Length != 3 || keys.Any(key => key != "-" && (key.Length != 11
            || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))))
            return (TelegramJobFilters.Empty, true);
        var filters = TelegramJobFilters.Empty;
        var changed = false;
        for (var i = 0; i < Fields.Length; i++)
        {
            if (keys[i] == "-") continue;
            if (!Enabled(settings, Fields[i])) { changed = true; continue; }
            var options = await OptionsAsync(Fields[i], en, ct);
            var matches = options.Where(option => FilterKey(option.Value) == keys[i]).ToArray();
            if (matches.Length != 1) { changed = true; continue; }
            filters = filters.With(Fields[i], matches[0].Value);
        }
        return (filters, changed);
    }

    private static string Summary(TelegramJobFilters filters, TelegramMemberSetting settings, bool en)
    {
        string Display(string field)
        {
            var value = filters.Value(field);
            if (value is null) return en ? "All" : "الكل";
            if (field == "city" && en)
                value = LocationSearch.Groups.FirstOrDefault(group => group[0] == value)?[1] ?? value;
            return value.Truncate(100);
        }
        return (en ? "🔎 Search filters" : "🔎 فلاتر البحث") + "\n\n"
            + string.Join("\n", Fields.Where(field => Enabled(settings, field)).Select(field => Label(field, en) + ": " + Display(field)));
    }

    private Task MenuAsync(long chat, TelegramJobFilters filters, TelegramMemberSetting settings, bool en, bool changed, CancellationToken ct)
    {
        var state = State(filters);
        var buttons = new[] { "category", "city", "organization" }.Where(field => Enabled(settings, field))
            .Select(field => (Label(field, en), "jobs:f:c:" + Code(field) + ":" + state + ":1")).ToList();
        buttons.Add((en ? "Show results" : "عرض النتائج", "jobs:f:r:" + state + ":1"));
        buttons.Add((en ? "Clear filters" : "مسح الفلاتر", "jobs:f:x"));
        var note = changed ? (en ? "A choice changed or was disabled. Review your filters before searching.\n\n"
            : "أحد الاختيارات لم يعد متاحًا أو تم تعطيله. راجع الفلاتر قبل عرض النتائج.\n\n") : "";
        return telegram.SendAsync(chat, note + Summary(filters, settings, en) + "\n\n"
            + (en ? "Choose any filters, then select Show results. Leave a filter on All to skip it."
                : "حدد الفلاتر التي تريدها، ثم اضغط عرض النتائج. اترك الفلتر على «الكل» إن لم ترغب بتحديده."), ct, callbacks: buttons.ToArray());
    }

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
        if (data is "jobs:m" or "jobs:f:x")
        { await MenuAsync(chat, TelegramJobFilters.Empty, settings, en, false, ct); return; }

        var parts = data.Split(':');
        // Previously issued one-criterion buttons reopen that choice in the new filter flow.
        if (parts.Length >= 4 && parts[0] == "jobs" && parts[1] != "f")
        {
            var oldField = parts[2];
            if (!Fields.Contains(oldField) || !Enabled(settings, oldField))
            { await MenuAsync(chat, TelegramJobFilters.Empty, settings, en, true, ct); return; }
            if (parts[1] == "c" && parts.Length == 4 && int.TryParse(parts[3], out var oldPage) && oldPage is >= 1 and <= 10000)
                parts = ["jobs", "f", "c", Code(oldField), "-.-.-", parts[3]];
            else if (parts[1] == "p" && parts.Length == 5)
            {
                var oldOptions = await OptionsAsync(oldField, en, ct);
                var matches = oldOptions.Where(option => Key(option.Value) == parts[3]).ToArray();
                await MenuAsync(chat, matches.Length == 1 ? TelegramJobFilters.Empty.With(oldField, matches[0].Value)
                    : TelegramJobFilters.Empty, settings, en, matches.Length != 1, ct);
                return;
            }
        }
        if (parts.Length < 4 || parts[0] != "jobs" || parts[1] != "f")
        { await MenuAsync(chat, TelegramJobFilters.Empty, settings, en, true, ct); return; }
        var action = parts[2];
        var stateIndex = action is "c" or "p" or "a" ? 4 : 3;
        if (parts.Length <= stateIndex)
        { await MenuAsync(chat, TelegramJobFilters.Empty, settings, en, true, ct); return; }
        var (filters, changed) = await ResolveAsync(parts[stateIndex], settings, en, ct);
        if (changed) { await MenuAsync(chat, filters, settings, en, true, ct); return; }
        var state = State(filters);
        if (action == "m" && parts.Length == 4)
        { await MenuAsync(chat, filters, settings, en, false, ct); return; }
        if (action == "r" && parts.Length == 5 && int.TryParse(parts[4], out var resultPage) && resultPage is >= 1 and <= 100)
        {
            var result = await search.FiltersAsync(member.UserId, filters, resultPage, ct);
            var buttons = new List<(string Text, string Data)>();
            if (resultPage > 1) buttons.Add((en ? "Previous jobs" : "الوظائف السابقة", "jobs:f:r:" + state + ":" + (resultPage - 1)));
            if (result.HasMore) buttons.Add((en ? "More jobs" : "وظائف أخرى", "jobs:f:r:" + state + ":" + (resultPage + 1)));
            buttons.Add((en ? "Edit filters" : "تعديل الفلاتر", "jobs:f:m:" + state));
            buttons.Add((en ? "Clear filters" : "مسح الفلاتر", "jobs:f:x"));
            await telegram.SendAsync(chat, Summary(filters, settings, en) + "\n\n" + result.Text, ct, callbacks: buttons.ToArray());
            return;
        }
        var field = Field(parts[3]);
        if (field is null || !Enabled(settings, field))
        { await MenuAsync(chat, filters, settings, en, true, ct); return; }
        if (action == "a" && parts.Length == 5)
        { await MenuAsync(chat, filters.With(field, null), settings, en, false, ct); return; }
        var options = await OptionsAsync(field, en, ct, filters);
        if (action == "c" && parts.Length == 6 && int.TryParse(parts[5], out var optionPage) && optionPage is >= 1 and <= 10000)
        {
            var pageCount = Math.Max(1, (options.Count + OptionsPerPage - 1) / OptionsPerPage);
            optionPage = Math.Min(optionPage, pageCount);
            var prefix = "jobs:f:c:" + Code(field) + ":" + state + ":";
            var buttons = options.Skip((optionPage - 1) * OptionsPerPage).Take(OptionsPerPage)
                .Select(option => (option.Label.Truncate(60), "jobs:f:p:" + Code(field) + ":" + state + ":" + FilterKey(option.Value))).ToList();
            buttons.Insert(0, (en ? "All" : "الكل", "jobs:f:a:" + Code(field) + ":" + state));
            if (optionPage > 1) buttons.Add((en ? "Previous options" : "الخيارات السابقة", prefix + (optionPage - 1)));
            if (optionPage < pageCount && optionPage < 10000) buttons.Add((en ? "More options" : "خيارات أخرى", prefix + (optionPage + 1)));
            buttons.Add((en ? "Back to filters" : "العودة للفلاتر", "jobs:f:m:" + state));
            await telegram.SendAsync(chat, Summary(filters, settings, en) + "\n\n" + Label(field, en) + " — "
                + (options.Count == 0 ? (en ? "No options match the other filters. Choose All or edit your filters."
                    : "لا توجد خيارات تطابق بقية الفلاتر. اختر الكل أو عدّل الفلاتر.")
                    : (en ? "Choose an option" : "اختر من القائمة") + " (" + optionPage + "/" + pageCount + ")"),
                ct, callbacks: buttons.ToArray());
            return;
        }
        if (action == "p" && parts.Length == 6)
        {
            var matches = options.Where(option => FilterKey(option.Value) == parts[5]).ToArray();
            await MenuAsync(chat, matches.Length == 1 ? filters.With(field, matches[0].Value) : filters,
                settings, en, matches.Length != 1, ct);
            return;
        }
        await MenuAsync(chat, filters, settings, en, true, ct);
    }

    public async Task<List<TelegramJobOption>> OptionsAsync(string field, bool en, CancellationToken ct, TelegramJobFilters? filters = null)
    {
        var query = await new TelegramJobSearch(db, configuration).FilteredAsync((filters ?? TelegramJobFilters.Empty).With(field, null), true, ct);
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
