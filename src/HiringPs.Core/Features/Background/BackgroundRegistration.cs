using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>
/// يسجل حلقات الخلفية (الجمع، فحص الأدوات، تنبيهات العضوية، النشر في القناة وIndexNow)
/// لتعمل داخل خدمة العامل أو داخل الموقع نفسه عند تفعيل Collector:RunInWeb.
/// </summary>
public static class BackgroundRegistration
{
    /// <summary>يحدد هل يشغّل الموقع حلقات الخلفية بنفسه بدل خدمة Windows المستقلة.</summary>
    public static bool RunInWeb(IConfiguration configuration) =>
        configuration.GetValue<bool?>("Collector:RunInWeb") ?? false;

    /// <summary>الخدمات التي تحتاجها الحلقات ولا تسجلها الواجهة؛ آمنة عند التكرار.</summary>
    public static IServiceCollection AddBackgroundDependencies(this IServiceCollection services)
    {
        // One Chromium process per host; each item still gets its own context and page.
        services.TryAddSingleton<BrowserHost>();
        services.TryAddSingleton<ToolWorkerRuntime>();
        services.TryAddScoped<TelegramCommands>();
        services.TryAddScoped<MemberNotifications>();
        return services;
    }

    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddBackgroundDependencies();
        services.AddHostedService<CollectionWorker>();
        services.AddHostedService<ToolMonitor>();
        services.AddHostedService<MembershipWorker>();
        services.AddHostedService<DistributionWorker>();
        return services;
    }
}
