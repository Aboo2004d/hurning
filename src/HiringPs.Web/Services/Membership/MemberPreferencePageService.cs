using HiringPs.Core;
using HiringPs.Web.Models;

namespace HiringPs.Web.Services.Membership;

/// <summary>يجمع كتالوج الواجهة ويطبق أوامر محرر التفضيلات دون الاعتماد على MVC أو ModelState.</summary>
public sealed class MemberPreferencePageService(MemberPreferenceService preferences)
{
    /// <summary>يضيف أسماء المدن القياسية إلى المواقع المعتمدة بنفس ترتيب ومقارنة المحرر السابق.</summary>
    private async Task FillAsync(PreferenceInput input, string owner, CancellationToken ct)
    {
        var options = await preferences.OptionsAsync(ct);
        input.CategoryLimit = await preferences.LimitAsync(owner, ct);
        input.AvailableCategories = options.Categories;
        input.AvailableLocations = options.ApprovedLocations.Concat(CityCatalog.ArabicNames).Distinct().OrderBy(x => x).ToArray();
    }

    /// <summary>يحضر اختيارات الحساب والكتالوج لصفحة التفضيلات.</summary>
    public async Task<PreferenceInput> ReadAsync(string owner, bool savedReminders, CancellationToken ct)
    {
        var pref = await preferences.ReadAsync(owner, ct);
        var input = new PreferenceInput
        {
            Kinds = Enum.GetValues<OpportunityKind>().Where(x => MembershipRules.Includes(pref.Kinds, x)).ToArray(),
            Categories = pref.Categories,
            Locations = pref.Locations,
            SavedReminders = savedReminders,
        };
        await FillAsync(input, owner, ct);
        return input;
    }

    /// <summary>يدمج بحث القائمة في التفضيلات بعد مطابقة القيم بكتالوجها؛ القيم غير المعروفة لا تحفظ.</summary>
    public async Task<PreferenceSaveOutcome> AddSearchAsync(string owner, OpportunityKind? kind, string? city, string? category, CancellationToken ct)
    {
        if (
            (kind is null && string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(category))
            || (kind is { } requested && !Enum.IsDefined(requested))
        )
            return PreferenceSaveOutcome.Invalid;
        var pref = await preferences.ReadAsync(owner, ct);
        var input = new PreferenceInput();
        await FillAsync(input, owner, ct);
        var categories = pref.Categories;
        var locations = pref.Locations;
        if (!string.IsNullOrWhiteSpace(category))
        {
            // Equivalent spellings share one listing filter; keep all that the catalog offers.
            var added = FilterLabels
                .Equivalents(category, input.AvailableCategories, FilterLabels.Category)
                .Where(input.AvailableCategories.Contains)
                .ToArray();
            if (added.Length == 0)
                return PreferenceSaveOutcome.Invalid;
            categories = categories.Concat(added).Distinct().ToArray();
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var location = input.AvailableLocations.Contains(city)
                ? city
                : CityCatalog.Find(city)?.Arabic;
            if (location is null || !input.AvailableLocations.Contains(location))
                return PreferenceSaveOutcome.Invalid;
            locations = locations.Append(location).Distinct().ToArray();
        }
        var kinds = pref.Kinds;
        if (kind is { } selected)
            kinds |= MembershipRules.Bit(selected);
        return await preferences.SaveAsync(owner, new(kinds, categories, locations, null), ct)
            ? PreferenceSaveOutcome.Saved : PreferenceSaveOutcome.LimitExceeded;
    }

    /// <summary>يطبق إضافة وحذف وتحديد الخيارات مع إبقاء القيم القديمة قابلة للإزالة بعد اختفائها من الكتالوج.</summary>
    public async Task<bool> EditAsync(string owner, PreferenceInput input, string? addCategory, string? removeCategory,
        string? addLocation, string? removeLocation, string? bulkAction, CancellationToken ct)
    {
        var pref = await preferences.ReadAsync(owner, ct);
        var priorCategories = pref.Categories;
        var priorLocations = pref.Locations;
        await FillAsync(input, owner, ct);
        if (
            new[] { addCategory, removeCategory, addLocation, removeLocation, bulkAction }.Count(
                x => x is not null
            ) > 1
        )
            return false;
        if (addCategory is not null)
        {
            var category = CategoryRules.Resolve(addCategory, input.AvailableCategories.Concat(priorCategories));
            if (!input.AvailableCategories.Contains(category) && !priorCategories.Contains(category) && !CategoryRules.ValidNew(category))
                return false;
            input.Categories = input.Categories.Append(category).DistinctBy(CategoryRules.Key).ToArray();
            // Only the explicitly submitted new category is allowed beyond the existing catalog.
            priorCategories = priorCategories.Append(category).ToArray();
        }
        if (removeCategory is not null)
            input.Categories = input.Categories.Where(x => x != removeCategory).ToArray();
        if (addLocation is not null)
        {
            if (!input.AvailableLocations.Contains(addLocation))
                return false;
            input.Locations = input.Locations.Append(addLocation).Distinct().ToArray();
        }
        if (removeLocation is not null)
            input.Locations = input.Locations.Where(x => x != removeLocation).ToArray();
        switch (bulkAction)
        {
            case null:
                break;
            case "select-categories":
                input.Categories = input
                    .Categories.Concat(input.AvailableCategories)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-categories":
                input.Categories = [];
                break;
            case "select-locations":
                input.Locations = input
                    .Locations.Concat(input.AvailableLocations)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-locations":
                input.Locations = [];
                break;
            default:
                return false;
        }
        // Keep previously chosen values removable even if the source catalog no longer lists them.
        if (
            input.Kinds.Any(x => !Enum.IsDefined(x))
            || input.Categories.Except(input.AvailableCategories.Concat(priorCategories)).Any()
            || input.Locations.Except(input.AvailableLocations.Concat(priorLocations)).Any()
        )
            return false;
        input.Categories = input.Categories.Distinct().ToArray();
        input.Locations = input.Locations.Distinct().ToArray();
        var kinds = input.Kinds.Aggregate(0, (mask, kind) => mask | MembershipRules.Bit(kind));
        input.LimitExceeded = !await preferences.SaveAsync(owner, new(kinds, input.Categories, input.Locations, input.SavedReminders), ct);
        return !input.LimitExceeded;
    }
}
