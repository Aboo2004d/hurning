using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public static class SocialRegistration
{
    public static IServiceCollection AddSocialPublishing(this IServiceCollection services)
    {
        services.AddScoped<SocialPublishing>();
        services.AddHttpClient("SocialProviders", client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        return services;
    }
}
