using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public static class SitePageDefaults
{
    public static bool IsEditable(string key) => SitePageKeys.IsEditable(key);

    private static readonly ResourceManager Resources = new(
        "HiringPs.Web.Resources.Labels",
        typeof(Labels).Assembly
    );

    public static SitePageViewModel Get(string key, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        string Text(string name) => Resources.GetString(name, culture) ?? name;
        return key == "about"
            ? new(
                Text("About"),
                string.Join(
                    "\n\n",
                    Text("AboutIntro"),
                    Text("AboutOfferTitle"),
                    Text("AboutOffer"),
                    Text("AboutSourcesTitle"),
                    Text("AboutSources")
                )
            )
            : new(
                Text("Privacy"),
                string.Join(
                    "\n\n",
                    Text("PrivacyText"),
                    Text("SourceLinkNote"),
                    Text("Contact"),
                    Text("ContactPrivacyText")
                )
            );
    }

    public static SitePageViewModel Resolve(string key, SitePageContentDto? page, string language)
    {
        var fallback = Get(key, language);
        var ar = language == "ar";
        var title = ar ? page?.TitleAr : page?.TitleEn;
        var body = ar ? page?.BodyAr : page?.BodyEn;
        return new(
            string.IsNullOrWhiteSpace(title) ? fallback.Title : title,
            string.IsNullOrWhiteSpace(body) ? fallback.Body : body
        );
    }
}
