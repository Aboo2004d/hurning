using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace HiringPs.Core;

/// <summary>قراءة وحفظ إعدادات كل منصة مع حماية التعديلات المتزامنة.</summary>
public sealed class RandomPublicationSettingsService(HiringDb db)
{
    /// <summary>يعرض الوجهات الثلاث؛ الإعداد غير المحفوظ معطل وعدده الافتراضي اثنان.</summary>
    public async Task<List<RandomPublicationSettingsDto>> ReadAsync(CancellationToken ct)
    {
        var settings = await db.Set<RandomPublicationSetting>().AsNoTracking().ToListAsync(ct);
        var connections = await db.SocialConnections.AsNoTracking().ToListAsync(ct);
        return RandomPublicationRules.Providers.Select(provider =>
        {
            var s = settings.SingleOrDefault(x => x.Provider == provider) ?? new() { Provider = provider };
            var c = connections.SingleOrDefault(x => x.Provider == provider);
            return new RandomPublicationSettingsDto(provider, s.Enabled, s.DailyCount, s.MaxRemainingDays,
                RandomPublicationRules.Times(s.TimesJson), Convert.ToBase64String(s.Version),
                c is { Enabled: true } && (c.ExpiresAt == null || c.ExpiresAt > DateTimeOffset.UtcNow), c?.PageName ?? "");
        }).ToList();
    }
    /// <summary>يحفظ وجهة واحدة فقط؛ يعيد رمز خطأ مترجم ولا يفعّل وجهة غير مربوطة.</summary>
    public async Task<string?> SaveAsync(RandomPublicationSettingsDto input, string actor, CancellationToken ct)
    {
        if (!RandomPublicationRules.Providers.Contains(input.Provider)
            || !RandomPublicationRules.Valid(input.DailyCount, input.MaxRemainingDays, input.Times)) return "RandomSettingsInvalid";
        var now = DateTimeOffset.UtcNow;
        if (input.Enabled && !await db.SocialConnections.AnyAsync(x => x.Provider == input.Provider && x.Enabled
            && (x.ExpiresAt == null || x.ExpiresAt > now), ct)) return "IntegrationNotConfigured";
        var row = await db.Set<RandomPublicationSetting>().FindAsync([input.Provider], ct);
        if (row is null)
        {
            if (input.Version.Length != 0) return "RandomSettingsConflict";
            row = new() { Provider = input.Provider };
            db.Add(row);
        }
        else
        {
            if (Convert.ToBase64String(row.Version) != input.Version) return "RandomSettingsConflict";
            db.Entry(row).Property(x => x.Version).OriginalValue = row.Version;
        }
        row.Enabled = input.Enabled; row.DailyCount = input.DailyCount;
        row.MaxRemainingDays = input.MaxRemainingDays; row.TimesJson = JsonSerializer.Serialize(input.Times);
        db.Audit.Add(OperationTracking.Create(OperationArea.Publishing, OperationOutcome.Succeeded, "RandomSettings",
            $"{row.Provider}; Enabled {row.Enabled}; Count {row.DailyCount}; Days {row.MaxRemainingDays}; Times {string.Join(',', input.Times)}", actor));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return "RandomSettingsConflict"; }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { return "RandomSettingsConflict"; }
        return null;
    }
}
