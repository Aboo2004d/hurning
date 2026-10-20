// إبلاغ محركات البحث عبر IndexNow بتغير صفحات الفرص العامة، على دفعات من العامل وليس داخل طلبات الويب.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون IndexNowSettings ضمن مجال Indexing؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class IndexNowSettings
{
    public const string DefaultEndpoint = "https://api.indexnow.org/indexnow";
    public const int MaxUrls = 10000;

    /// <summary>ينفذ IsKeyFormat وفق قواعد IndexNowSettings الحالية دون تغيير عقد المستدعين.</summary>
    public static bool IsKeyFormat(string? key) =>
        key is { Length: >= 8 and <= 128 } && Regex.IsMatch(key, "^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant);

    /// <summary>The configured key, or null when missing or invalid. Served at /{key}.txt.</summary>
    public static string? Key(IConfiguration configuration)
    {
        var key = configuration["IndexNow:Key"]?.Trim();
        return IsKeyFormat(key) ? key : null;
    }

    /// <summary>ينفذ Enabled وفق قواعد IndexNowSettings الحالية دون تغيير عقد المستدعين.</summary>
    public static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("IndexNow:Enabled") && Key(configuration) is not null;
}

/// <summary>Called from HiringDb.SaveChanges to queue public-page changes. No network work here.</summary>
