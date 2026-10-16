using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class CheckServices
{
    // Match the worker registrations without requiring production credentials in fixture checks.
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HiringDb>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Hiring"))
        );
        services.AddSingleton<UrlPolicy>();
        services.AddScoped<AttachmentReader>();
        services.AddScoped<BrowserCollector>();
        services.AddScoped<OpportunityService>();
        services.AddScoped<CollectionPipeline>();
        services.AddHttpClient<AiExtractor>();
    }
}
