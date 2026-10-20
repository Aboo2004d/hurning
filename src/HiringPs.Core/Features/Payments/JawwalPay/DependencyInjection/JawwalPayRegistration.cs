using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>يسجل تبعيات محول جوال باي وسياسة HTTP الخاصة به في حاوية الخدمات.</summary>
public static class JawwalPayRegistration
{
    /// <summary>يسجل عميل جوال باي ومحول الدفع مع منع إعادة توجيه طلبات المزود.</summary>
    public static IServiceCollection AddJawwalPay(this IServiceCollection services)
    {
        services.TryAddScoped<IPaymentGatewayData, PaymentGatewayData>();
        services
            .AddHttpClient<JawwalPayGateway>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            )
            .RemoveAllLoggers();
        services.AddScoped<IPaymentGateway>(provider =>
            provider.GetRequiredService<JawwalPayGateway>()
        );
        return services;
    }
}
