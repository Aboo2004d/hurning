// إنشاء العناوين والوصف والروابط الأساسية والبيانات المنظمة للصفحات العامة.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>Stable, localized public URLs and metadata; never derives the origin from request headers.</summary>
public sealed class SiteSeo
{
    public string Origin { get; }

    public const int SitemapPageSize = 10000;

    public SiteSeo(IConfiguration configuration)
    {
        var value = configuration["Seo:PublicOrigin"] ?? "https://hiring.ps";
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
        )
            throw new InvalidOperationException(
                "Seo:PublicOrigin must be an HTTPS origin without a path, port or credentials."
            );
        Origin = uri.GetLeftPart(UriPartial.Authority);
    }

    public string Url(
        string path,
        bool english,
        IEnumerable<KeyValuePair<string, string?>>? query = null
    )
    {
        path = HiringPs.Web.Infrastructure.LanguageUrls.WithoutLanguage(path);
        var values = (query ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Value) && x.Key is not ("culture" or "ui-culture"))
            .ToList();
        return Origin + (english ? "/en" : "/ar") + path + QueryString.Create(values).ToUriComponent();
    }

    public SeoMetadata Build(HttpRequest request, object? model, string title, string description)
    {
        var english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        var brand = english ? "Employment in Palestine" : "التوظيف في فلسطين";
        var controller = request.RouteValues["controller"]?.ToString();
        var action = request.RouteValues["action"]?.ToString();
        var isHome = string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase);
        var path = request.Path.Value ?? "/";
        var values = new List<KeyValuePair<string, string?>>();
        var noindex = !isHome;
        string? previous = null,
            next = null;
        var pageType = "WebPage";
        var name = title;
        var breadcrumbs = new List<(string Name, string Url)> { (brand, Url("/", english)) };
        if (isHome && model is ListingModel listing)
        {
            path = "/";
            if (listing.Kind is { } kind)
                values.Add(new("kind", kind.ToString()));
            if (listing.Archive)
                values.Add(new("archive", "true"));
            foreach (
                var (key, value) in new[]
                {
                    ("q", listing.Query),
                    ("city", listing.City),
                    ("category", listing.Category),
                    ("employment", listing.Employment),
                    ("experience", listing.Experience),
                }
            )
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(new(key, value));
            if (listing.Sort != ListingSort.Newest)
                values.Add(new("sort", listing.Sort));
            var scope =
                listing.Kind is { } selected ? KindName(selected, english)
                : english ? "Jobs, training and tenders in Palestine"
                : "وظائف وتدريب وعطاءات في فلسطين";
            name = listing.Archive ? (english ? "Archive: " : "أرشيف: ") + scope : scope;
            if (listing.City.Length > 0)
                name += " · " + CityCatalog.Display(listing.City, english);
            if (listing.Category.Length > 0)
                name += " · " + FilterLabels.Category(listing.Category, english);
            if (listing.Query.Length > 0)
                name = (english ? "Search: " : "بحث: ") + listing.Query + " · " + name;
            if (listing.Page > 1)
                name += (english ? " · Page " : " · صفحة ") + listing.Page;
            // Filter, search, sort and archive pages stay noindex; kind × city landing pages are indexable instead.
            noindex =
                listing.Archive
                || listing.Count == 0
                || values.Any(x =>
                    x.Key is "q" or "city" or "category" or "employment" or "experience" or "sort"
                );
            Paginate(listing, english);
            pageType = "CollectionPage";
        }
        else if (controller == "Browse" && model is BrowseModel browse)
        {
            path = browse.Path;
            name = browse.Heading;
            description = browse.Intro;
            noindex = browse.Count == 0;
            if (browse.Kind is { } browseKind)
                breadcrumbs.Add(
                    (KindName(browseKind, english), Url("/", english, [new("kind", browseKind.ToString())]))
                );
            if (browse.Page > 1)
                name += (english ? " · Page " : " · صفحة ") + browse.Page;
            Paginate(browse, english);
            pageType = "CollectionPage";
        }
        else if (controller == "Opportunities" && model is Opportunity opportunity)
        {
            path = PublicListing.DetailsPath(opportunity);
            name = string.Join(
                " · ",
                new[] { opportunity.Title, opportunity.Organization, opportunity.Locations }.Where(
                    x => !string.IsNullOrWhiteSpace(x)
                )
            );
            description = string.Join(
                " — ",
                new[]
                {
                    opportunity.Summary,
                    opportunity.Locations,
                    opportunity.Deadline is { } deadline
                        ? (english ? "Apply by: " : "آخر موعد: ") + deadline.ToString("yyyy-MM-dd")
                        : "",
                }.Where(x => x.Length > 0)
            );
            noindex =
                opportunity.Status != PublicationStatus.Published
                || opportunity.Deadline is { } date && date < TextRules.PalestineToday();
            breadcrumbs.Add(
                (
                    KindName(opportunity.Kind, english),
                    Url("/", english, [new("kind", opportunity.Kind.ToString())])
                )
            );
        }
        else if (controller is "About" or "Contact" or "Privacy")
        {
            path = "/" + controller;
            noindex = false;
            pageType =
                controller == "About" ? "AboutPage"
                : controller == "Contact" ? "ContactPage"
                : "WebPage";
        }
        else
            noindex = true;

        void Paginate(ListingModel list, bool englishUrls)
        {
            if (list.Page > 1)
                previous = Url(path, englishUrls, WithPage(values, list.Page - 1));
            if (list.Page < list.PageCount)
                next = Url(path, englishUrls, WithPage(values, list.Page + 1));
            if (list.Page > 1)
                values.Add(new("page", list.Page.ToString(CultureInfo.InvariantCulture)));
        }

        var canonical = Url(path, english, values);
        if (
            request.HttpContext.Response.StatusCode >= 400
            || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        )
            noindex = true;
        description = Snippet(description, 170);
        var fullTitle = Snippet(name, 160) + " | " + brand;
        var graph = new List<object>();
        if (!noindex)
        {
            graph.Add(
                new Dictionary<string, object?>
                {
                    ["@type"] = "Organization",
                    ["@id"] = Origin + "/#organization",
                    ["name"] = "التوظيف في فلسطين",
                    ["alternateName"] = new[] { "Employment in Palestine", "hiring.ps" },
                    ["url"] = Origin + "/",
                    ["logo"] = Origin + "/images/brand/basket-logo.png",
                }
            );
            graph.Add(
                new Dictionary<string, object?>
                {
                    ["@type"] = "WebSite",
                    ["@id"] = Origin + "/#website",
                    ["url"] = Origin + "/",
                    ["name"] = brand,
                    ["inLanguage"] = new[] { "ar", "en" },
                    ["publisher"] = Ref(Origin + "/#organization"),
                }
            );
            if (breadcrumbs[^1].Url != canonical)
                breadcrumbs.Add((Snippet(name, 160), canonical));
            graph.Add(
                new Dictionary<string, object?>
                {
                    ["@type"] = "BreadcrumbList",
                    ["@id"] = canonical + "#breadcrumbs",
                    ["itemListElement"] = breadcrumbs
                        .Select(
                            (item, index) =>
                                new Dictionary<string, object?>
                                {
                                    ["@type"] = "ListItem",
                                    ["position"] = index + 1,
                                    ["name"] = item.Name,
                                    ["item"] = item.Url,
                                }
                        )
                        .ToArray(),
                }
            );
            var webpage = new Dictionary<string, object?>
            {
                ["@type"] = pageType,
                ["@id"] = canonical + "#webpage",
                ["url"] = canonical,
                ["name"] = fullTitle,
                ["description"] = description,
                ["inLanguage"] = english ? "en" : "ar",
                ["isPartOf"] = Ref(Origin + "/#website"),
                ["breadcrumb"] = Ref(canonical + "#breadcrumbs"),
            };
            if (model is ListingModel list)
            {
                webpage["mainEntity"] = new Dictionary<string, object?>
                {
                    ["@type"] = "ItemList",
                    // Only items whose details page is public; restricted details are not linked for crawlers.
                    ["itemListElement"] = list
                        .Items.Where(item => list.PublicDetailKinds.Contains(item.Kind))
                        .Select(
                            (item, index) =>
                                new Dictionary<string, object?>
                                {
                                    ["@type"] = "ListItem",
                                    ["position"] = (list.Page - 1) * PublicListing.PageSize + index + 1,
                                    ["name"] = item.Title,
                                    ["url"] = Url(PublicListing.DetailsPath(item), english),
                                }
                        )
                        .ToArray(),
                };
            }

            if (model is SitePageViewModel about && controller == "About" && about.Questions.Count > 0)
            {
                webpage["hasPart"] = new Dictionary<string, object?>
                {
                    ["@type"] = "FAQPage",
                    ["@id"] = canonical + "#common-questions",
                    ["mainEntity"] = about.Questions
                        .Select(item => new Dictionary<string, object?>
                        {
                            ["@type"] = "Question",
                            ["name"] = item.Question,
                            ["acceptedAnswer"] = new Dictionary<string, object?>
                            {
                                ["@type"] = "Answer",
                                ["text"] = item.Answer,
                            },
                        })
                        .ToArray(),
                };
            }

            graph.Add(webpage);
        }

        // Default JSON escaping is deliberate: source text must never close the script element.
        var json =
            graph.Count == 0
                ? ""
                : JsonSerializer.Serialize(
                    new Dictionary<string, object>
                    {
                        ["@context"] = "https://schema.org",
                        ["@graph"] = graph,
                    }
                );
        return new(
            fullTitle,
            description,
            canonical,
            Url(path, false, values),
            Url(path, true, values),
            noindex ? "noindex, follow" : "index, follow, max-image-preview:large",
            english ? "en" : "ar",
            json,
            previous,
            next
        );
    }

    private static object Ref(string id) => new Dictionary<string, string> { ["@id"] = id };

    private static IEnumerable<KeyValuePair<string, string?>> WithPage(
        IEnumerable<KeyValuePair<string, string?>> values,
        int page
    ) =>
        page > 1 ? values.Append(new("page", page.ToString(CultureInfo.InvariantCulture))) : values;

    public static string Snippet(string value, int length)
    {
        var text = Regex.Replace(value, @"\s+", " ").Trim();
        var elements = StringInfo.ParseCombiningCharacters(text);
        return elements.Length > length ? text[..elements[length - 1]].TrimEnd() + "…" : text;
    }

    private static string KindName(OpportunityKind kind, bool english) =>
        (kind, english) switch
        {
            (OpportunityKind.Job, false) => "وظائف في فلسطين",
            (OpportunityKind.Training, false) => "فرص تدريب في فلسطين",
            (OpportunityKind.Tender, false) => "عطاءات في فلسطين",
            (OpportunityKind.ProcurementNotice, false) => "مناقصات في فلسطين",
            (OpportunityKind.Job, true) => "Jobs in Palestine",
            (OpportunityKind.Training, true) => "Training in Palestine",
            (OpportunityKind.Tender, true) => "Tenders in Palestine",
            _ => "Procurement notices in Palestine",
        };
}
