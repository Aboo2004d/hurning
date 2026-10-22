using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>Registers scoped administration services alongside the request's existing HiringDb.</summary>
public static class AdministrationRegistration
{
    /// <summary>Registers read/write features without starting workers or calling external providers.</summary>
    public static IServiceCollection AddAdministrationFeatures(this IServiceCollection services)
    {
        services.TryAddScoped<AdminMemberLookupService>();
        services.TryAddScoped<ContactInboxService>();
        services.TryAddScoped<ReportManagementService>();
        services.TryAddScoped<MergeAdministrationService>();
        services.TryAddScoped<NotificationAdministrationService>();
        services.TryAddScoped<SourceItemAdministrationService>();
        services.TryAddScoped<AdminDashboardReadService>();
        services.TryAddScoped<AiUsageReadService>();
        services.TryAddScoped<AiPauseReadService>();
        services.TryAddScoped<SiteContentService>();
        services.TryAddScoped<RetentionSettingsService>();
        services.TryAddScoped<PromptAdministrationService>();
        services.TryAddScoped<CollectorDiagnosticReadService>();
        services.TryAddScoped<OperationReadService>();
        services.TryAddScoped<CategoryGroupReadService>();
        services.TryAddScoped<CategoryGroupManagementService>();
        services.TryAddScoped<CategoryGroupSuggestionService>();
        services.TryAddScoped<ICategoryCatalogReadService, CategoryCatalogService>();
        return services;
    }
}
