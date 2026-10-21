using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>تسجيل مسجل الأخطاء المشترك في الويب والعامل.</summary>
public static class DatabaseErrorLoggingRegistration
{
    /// <summary>يضيف التقاط الأخطاء دون تغيير مقدمي سجلات التشغيل الحاليين.</summary>
    public static IServiceCollection AddDatabaseErrorLogging(this IServiceCollection services, string application)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ErrorLogSanitizer>();
        services.AddSingleton<DatabaseErrorLogging>(sp => new(sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<ErrorLogSanitizer>(), application));
        services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<DatabaseErrorLogging>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DatabaseErrorLogging>());
        return services;
    }
}
