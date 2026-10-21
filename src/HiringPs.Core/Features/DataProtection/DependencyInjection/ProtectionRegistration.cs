using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>مفاتيح حماية دائمة ومشتركة في القاعدة فقط للويب والعامل.</summary>
public static class ProtectionRegistration
{
    /// <summary>يستخدم اتصال Hiring الحالي؛ إطار الحماية ينشئ المفاتيح ويجددها تلقائيًا داخل SQL.</summary>
    public static IDataProtectionBuilder AddHiringDataProtection(this IServiceCollection services)
    {
        var protection = services.AddDataProtection().SetApplicationName("HiringPs");
        services.AddSingleton<SqlProtectionKeyRepository>();
        services.AddSingleton<IXmlRepository>(sp => sp.GetRequiredService<SqlProtectionKeyRepository>());
        services.AddOptions<KeyManagementOptions>().Configure<IXmlRepository>((options, repository) =>
        {
            options.XmlRepository = repository;
            // Avoid user/machine DPAPI: the worker and website can run under different identities.
            // Protect database access and backups; key XML must never be exposed through the UI or logs.
            options.XmlEncryptor = null;
        });
        return protection;
    }
}
