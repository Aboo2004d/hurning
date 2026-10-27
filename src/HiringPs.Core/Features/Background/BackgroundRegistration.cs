using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>يسجل اعتماديات الجمع منفصلة عن حلقات خدمات السيرفر الخفيفة؛ لا يبدأ جمعًا تلقائيًا.</summary>
public static class BackgroundRegistration
{
    /// <summary>الموقع لا يبدأ الجمع تلقائيًا؛ وضع السيرفر يشغل الخدمات الخفيفة فقط.</summary>
    public static bool RunInWeb(IConfiguration configuration) => ApplicationRuntime.Mode(configuration) == ApplicationRuntimeMode.Server;

    /// <summary>يشغل تسليم Telegram والدفع والنشر وIndexNow على السيرفر دون أي متصفح أو جمع.</summary>
    public static IServiceCollection AddServerWorkers(this IServiceCollection services)
    {
        services.AddBackgroundDependencies();
        services.AddHostedService<MembershipWorker>();
        services.AddHostedService<DistributionWorker>();
        return services;
    }

    /// <summary>يسجل اعتماديات الجمع والفحص دون تشغيل أي مهمة خلفية.</summary>
    public static IServiceCollection AddBackgroundDependencies(this IServiceCollection services)
    {
        services.TryAddSingleton<BrowserHost>();
        services.TryAddSingleton<ToolWorkerRuntime>();
        services.TryAddScoped<TelegramCommands>();
        services.TryAddScoped<MemberNotifications>();
        return services;
    }
}
