using Microsoft.EntityFrameworkCore;
namespace HiringPs.Core;

/// <summary>Reads and audits the free-account quota without rewriting existing preferences or plan versions.</summary>
public sealed class MemberPreferenceSettingsService(HiringDb db)
{
    public async Task<int> ReadAsync(CancellationToken ct) =>
        await db.MemberPreferenceSettings.AsNoTracking().Where(x => x.Id == 1)
            .Select(x => (int?)x.NonSubscriberCategoryLimit).SingleOrDefaultAsync(ct)
        ?? PreferenceLimitRules.NonSubscriberLimit;

    public async Task<bool> SaveAsync(int limit, string actor, CancellationToken ct)
    {
        if (limit is < 0 or > 1000) return false;
        var setting = await db.MemberPreferenceSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        var previous = setting?.NonSubscriberCategoryLimit ?? PreferenceLimitRules.NonSubscriberLimit;
        if (setting is null) { setting = new(); db.MemberPreferenceSettings.Add(setting); }
        setting.NonSubscriberCategoryLimit = limit;
        new MembershipService(db).Audit(actor, "Membership.FreePreferenceLimit", $"Previous {previous}; New {limit}");
        await db.SaveChangesAsync(ct);
        return true;
    }
}
