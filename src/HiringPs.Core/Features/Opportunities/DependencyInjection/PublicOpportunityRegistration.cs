using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>تسجيل قراءات الزوار مع سياق البيانات scoped للطلب نفسه.</summary>
public static class PublicOpportunityRegistration
{
    /// <summary>يسجل الخدمات فقط؛ لا يبدأ جمعًا أو نشرًا أو اتصالًا خارجيًا.</summary>
    public static IServiceCollection AddPublicOpportunityFeatures(this IServiceCollection services)
    {
        services.TryAddScoped<OpportunityDetailsReadService>();
        services.TryAddScoped<OpportunityReportService>();
        services.TryAddScoped<PublicSitemapReadService>();
        return services;
    }
}
