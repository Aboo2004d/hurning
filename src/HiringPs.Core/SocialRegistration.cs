using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public static class SocialRegistration
{
    public static IServiceCollection AddSocialPublishing(this IServiceCollection services)
    {
        services.AddScoped<SocialPublishing>();
        services.AddScoped<SocialTemplates>();
        services.AddHttpClient("SocialProviders", client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        // Telegram channel and IndexNow: request URIs contain the bot token or key, so no HTTP logging.
        services.AddScoped<TelegramChannelPublisher>();
        services.AddScoped<IndexNowSubmitter>();
        services.AddHttpClient<TelegramChannelClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.AddHttpClient(IndexNowSubmitter.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        return services;
    }
}
