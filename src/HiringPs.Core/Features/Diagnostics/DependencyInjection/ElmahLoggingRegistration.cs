using ElmahCore;
using ElmahCore.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يسجل ELMAH وتخزين SQL الأصلي فقط؛ لا يضيف أي ILoggerProvider.</summary>
public static class ElmahLoggingRegistration
{
    public const string Path = "/elmah";

    /// <summary>يهيئ التقاط استثناءات الويب عبر ELMAH؛ إنشاء الجدول مسؤولية الماجريشن فقط.</summary>
    public static IServiceCollection AddHiringElmah(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ErrorLogSanitizer>();
        services.AddSingleton<ElmahErrorPolicy>();
        // Register the native store directly; AddElmah() would also add an ILogger provider.
        services.AddSingleton<ErrorLog, SqlErrorLog>();
        services.AddOptions<ElmahOptions>().Configure<IConfiguration, ElmahErrorPolicy>((options, config, policy) =>
        {
            options.Path = Path;
            options.ApplicationName = "HiringPs";
            options.ConnectionString = config.GetConnectionString("Hiring")!;
            options.SqlServerDatabaseSchemaName = "dbo";
            options.SqlServerDatabaseTableName = "ELMAH_Error";
            options.LogRequestBody = false;
            options.OnPermissionCheck = context => context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Admin");
            options.OnError = (context, error) =>
            {
                policy.Describe(error, context);
                return Task.CompletedTask;
            };
        });
        return services;
    }
}
