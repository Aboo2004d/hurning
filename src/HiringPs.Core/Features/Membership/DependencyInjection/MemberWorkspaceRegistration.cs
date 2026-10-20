using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HiringPs.Core;

/// <summary>تسجيل خدمات حساب العضو والمحفظوظات والتفضيلات في نطاق الطلب نفسه.</summary>
public static class MemberWorkspaceRegistration
{
    /// <summary>يسجل خدمات المساحة الشخصية دون تفعيل اشتراك أو إرسال رسالة عند التسجيل.</summary>
    public static IServiceCollection AddMemberWorkspaceFeatures(this IServiceCollection services)
    {
        services.TryAddScoped<SavedOpportunityService>();
        services.TryAddScoped<MemberAccountReadService>();
        services.TryAddScoped<MemberPreferenceService>();
        services.TryAddScoped<MemberNotificationSettingsService>();
        return services;
    }
}
