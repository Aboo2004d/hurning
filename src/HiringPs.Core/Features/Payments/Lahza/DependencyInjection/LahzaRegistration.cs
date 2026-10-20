using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>يسجل تبعيات محول لحظة وسياسة HTTP الخاصة به في حاوية الخدمات.</summary>
public static class LahzaRegistration
{
    /// <summary>يسجل عميل لحظة ومحول الدفع مع مهلة محددة وتعطيل إعادة التوجيه التلقائي.</summary>
    public static IServiceCollection AddLahza(this IServiceCollection services)
    {
        services.TryAddScoped<IPaymentGatewayData, PaymentGatewayData>();
        services
            .AddHttpClient<LahzaGateway>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            )
            .RemoveAllLoggers();
        services.AddScoped<IPaymentGateway>(provider =>
            provider.GetRequiredService<LahzaGateway>()
        );
        return services;
    }
}
