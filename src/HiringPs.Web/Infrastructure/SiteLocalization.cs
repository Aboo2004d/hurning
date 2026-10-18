using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace HiringPs.Web.Infrastructure;

public static class SiteLocalization
{
    public static RequestLocalizationOptions Options()
    {
        // Opportunity dates are Gregorian in storage, evidence and both interface languages.
        var arabic = new CultureInfo("ar");
        arabic.DateTimeFormat.Calendar = new GregorianCalendar();
        var english = new CultureInfo("en");
        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(arabic),
            SupportedCultures = [arabic, english],
            SupportedUICultures = [arabic, english],
            RequestCultureProviders =
            [
                new CustomRequestCultureProvider(context =>
                    Task.FromResult<ProviderCultureResult?>(
                        context.Items["Language"] is string language
                            ? new ProviderCultureResult(language)
                            : null
                    )
                ),
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider(),
            ],
        };
    }
}
