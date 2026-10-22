using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;

namespace HiringPs.Web.Infrastructure;

// Language is an application path prefix. PathBase lets every MVC URL keep it,
// including forms, pagination, static resources and installations under a subpath.
public static class LanguageUrls
{
    public static bool IsSupported(string? value) => value is "ar" or "en";

    public static string WithoutLanguage(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && (IsSupported(segments[0]) || IsMistypedLanguage(segments[0])))
            return segments.Length == 1 ? "/" : "/" + string.Join('/', segments.Skip(1));
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static bool IsMistypedLanguage(string value) =>
        value.Length == 2 && value.All(char.IsAsciiLetter);

    public static string? Saved(HttpContext context)
    {
        var cookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
        return CookieRequestCultureProvider
            .ParseCookieValue(cookie ?? "")
            ?.UICultures.FirstOrDefault()
            .Value;
    }

    public static string Select(HttpContext context)
    {
        var saved = Saved(context);
        if (IsSupported(saved))
            return saved!;
        Microsoft.Net.Http.Headers.StringWithQualityHeaderValue.TryParseList(
            context.Request.Headers.AcceptLanguage.Select(value => value ?? "").ToArray(),
            out var preferences
        );
        var requested = preferences
            ?.OrderByDescending(x => x.Quality ?? 1)
            .Where(x => (x.Quality ?? 1) > 0)
            .Select(x => x.Value.ToString().Split('-')[0].ToLowerInvariant())
            .FirstOrDefault(IsSupported);
        return requested ?? "ar";
    }

    public static void Save(HttpContext context, string culture)
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            }
        );
    }

    public static string Switch(string destination, PathString mountPath, string culture)
    {
        var uri = new Uri(new Uri("https://local.invalid"), destination);
        var path = uri.AbsolutePath;
        var mount = mountPath.Value?.TrimEnd('/') ?? "";
        if (
            mount.Length > 0
            && (path == mount || path.StartsWith(mount + "/", StringComparison.OrdinalIgnoreCase))
        )
            path = path[mount.Length..];
        var query = QueryHelpers.ParseQuery(uri.Query);
        query.Remove("culture");
        query.Remove("ui-culture");
        return mount
            + "/"
            + culture
            + WithoutLanguage(path)
            + QueryString.Create(query)
            + uri.Fragment;
    }

    public static bool IsStaticAsset(PathString path) =>
        path.StartsWithSegments("/images")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/vendor")
        || path.Value is "/favicon.ico";

    public static bool IsInfrastructure(PathString path) =>
        path.StartsWithSegments(HiringPs.Core.ElmahLoggingRegistration.Path)
        || path.StartsWithSegments("/signin-google")
        || path.StartsWithSegments("/images")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/vendor")
        || path.StartsWithSegments("/feeds")
        || path.StartsWithSegments("/sitemaps")
        || path.StartsWithSegments("/integrations")
        || path.Value is "/robots.txt" or "/sitemap.xml" or "/llms.txt" or "/favicon.ico"
        || IsRootKeyFile(path.Value);

    // IndexNow key file: /{key}.txt with the key's own character set; served without a language prefix.
    private static bool IsRootKeyFile(string? path) =>
        path is { Length: > 5 }
        && path.EndsWith(".txt", StringComparison.Ordinal)
        && path.LastIndexOf('/') == 0
        && HiringPs.Core.IndexNowSettings.IsKeyFormat(path[1..^4]);
}

public sealed class LanguagePathMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (LanguageUrls.IsInfrastructure(request.Path))
        {
            await next(context);
            return;
        }
        var originalBase = request.PathBase;
        var originalPath = request.Path;
        var first = request
            .Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (!LanguageUrls.IsSupported(first))
        {
            var target =
                originalBase
                + "/"
                + LanguageUrls.Select(context)
                + LanguageUrls.WithoutLanguage(originalPath.Value ?? "/")
                + request.QueryString;
            context.Response.Redirect(target, permanent: false, preserveMethod: true);
            return;
        }
        context.Items["MountPath"] = originalBase;
        context.Items["Language"] = first;
        // Only rewrite the culture cookie when the URL language differs from the saved one.
        if (LanguageUrls.Saved(context) != first)
            LanguageUrls.Save(context, first!);
        request.PathBase = originalBase.Add(new PathString("/" + first));
        request.Path = new PathString(LanguageUrls.WithoutLanguage(originalPath.Value!));
        try
        {
            await next(context);
        }
        finally
        {
            request.PathBase = originalBase;
            request.Path = originalPath;
        }
    }
}
