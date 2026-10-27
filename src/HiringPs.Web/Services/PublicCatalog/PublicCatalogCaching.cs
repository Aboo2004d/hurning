using Microsoft.AspNetCore.OutputCaching;

namespace HiringPs.Web.Models;

/// <summary>سياسات كاش الصفحة العامة مرتبطة بنسخة البيانات نفسها المستخدمة في العدادات والفلاتر.</summary>
public static class PublicCatalogCaching
{
    /// <summary>يحافظ على مهلة احتياطية مع تجاوز نسخة الصفحة القديمة فور إبطال البيانات.</summary>
    public static void Configure(OutputCacheOptions options)
    {
        options.AddPolicy(
            SiteCache.Files,
            policy => policy.Expire(TimeSpan.FromMinutes(10)).SetVaryByQuery("*")
                .VaryByValue(async (context, ct) =>
                {
                    if (context.RequestServices.GetService<PublicCatalogRevisionMonitor>() is { } monitor) await monitor.RefreshAsync(ct);
                    return new KeyValuePair<string, string>("catalog", context.RequestServices.GetRequiredService<PublicCatalogCache>().Snapshot().Version);
                })
        );
        options.AddPolicy(
            SiteCache.Listing,
            policy =>
                policy
                    .Expire(TimeSpan.FromSeconds(60))
                    .SetVaryByQuery("*")
                    .VaryByValue(async (context, ct) =>
                {
                    if (context.RequestServices.GetService<PublicCatalogRevisionMonitor>() is { } monitor) await monitor.RefreshAsync(ct);
                    return new KeyValuePair<string, string>("catalog", context.RequestServices.GetRequiredService<PublicCatalogCache>().Snapshot().Version);
                })
                    .VaryByValue(context =>
                        new(
                            "culture",
                            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                        )
                    )
        );
    }
}
