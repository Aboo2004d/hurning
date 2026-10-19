namespace HiringPs.Web.Infrastructure;

public static class SiteAntiforgery
{
    // A distinct, stable name avoids selecting legacy same-name cookies scoped to /ar or /en.
    public const string CookieName = "HiringPs.Antiforgery";

    public static IServiceCollection AddSiteAntiforgery(
        this IServiceCollection services,
        bool development
    )
    {
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
        return services;
    }
}
