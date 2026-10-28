using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>يفصل حلقات خدمات السيرفر عن مستهلك طلبات الجمع المحلية واعتمادياتهما.</summary>
public static class BackgroundRegistration
{
    /// <summary>يحدد تشغيل خدمات السيرفر؛ الوضع المحلي يستهلك الطلبات اليدوية فقط.</summary>
    public static bool RunInWeb(IConfiguration configuration) => ApplicationRuntime.Mode(configuration) == ApplicationRuntimeMode.Server;

    /// <summary>يشغل تسليم Telegram والدفع والنشر وIndexNow على السيرفر دون أي متصفح أو جمع.</summary>
    public static IServiceCollection AddServerWorkers(this IServiceCollection services)
    {
        services.AddBackgroundDependencies();
        services.AddHostedService<MembershipWorker>();
        services.AddHostedService<DistributionWorker>();
        return services;
    }

    /// <summary>يشغل مستهلك الطلبات اليدوية ونبضات العامل والفحوص المطلوبة في الويب المحلي فقط.</summary>
    public static IServiceCollection AddLocalCollectionWorkers(this IServiceCollection services)
    {
        services.AddBackgroundDependencies();
        services.AddHostedService<CollectionWorker>();
        services.AddHostedService<ToolMonitor>();
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
