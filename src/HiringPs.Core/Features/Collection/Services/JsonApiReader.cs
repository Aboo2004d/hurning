using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>Shared, bounded GET-only reader for administrator-configured public JSON APIs.</summary>
public sealed class JsonApiReader : IDisposable
{
    private readonly UrlPolicy policy;
    private readonly HttpClient http;
    private readonly bool ownsClient;

    public JsonApiReader(UrlPolicy policy, HttpClient? client = null)
    {
        this.policy = policy;
        ownsClient = client is null;
        http = client ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = async (context, ct) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                if (addresses.Length == 0 || addresses.Any(UrlPolicy.IsPrivate))
                    throw new InvalidOperationException("API target is not public.");
                var address = addresses.OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).First();
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, true);
                }
                catch { socket.Dispose(); throw; }
            }
        }) { Timeout = TimeSpan.FromSeconds(40) };
    }

    public static SourceConfiguration Configuration(Source source)
    {
        var conf = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson)
            ?? throw new InvalidOperationException("Invalid API configuration.");
        Validate(source.Url, conf);
        return conf;
    }

    public static void Validate(string sourceUrl, SourceConfiguration conf)
    {
        var api = conf.Api ?? throw new InvalidOperationException("API mapping is required.");
        if (conf.MaxPages is < 1 or > 50 || api.Fields is null || api.Fields.Length is < 1 or > 40)
            throw new InvalidOperationException("Invalid API bounds or fields.");
        if (!api.ListingUrlTemplate.Contains("{page}", StringComparison.Ordinal))
            throw new InvalidOperationException("API listing template requires {page}.");
        CheckUrl(PageUrl(api, 1), conf, sourceUrl);
        foreach (var template in new[] { api.ItemUrlTemplate, api.DetailUrlTemplate })
        {
            if (!Regex.IsMatch(template, @"\{(?:id|slug)\}"))
                throw new InvalidOperationException("API detail templates require {id} or {slug}.");
            // Identity placeholders must be path segments, never hosts or query parameters.
            var uri = CheckUrl(Expand(template, "123", "sample-slug"), conf, sourceUrl);
            if (!template.StartsWith(uri.GetLeftPart(UriPartial.Authority) + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("API identity placeholders must be complete path segments.");
            if (!TemplatePathIsSafe(template))
                throw new InvalidOperationException("API identity placeholders must be complete path segments.");
        }
        foreach (var path in new[] { api.ItemsPath, api.DetailPath, api.IdPath, api.ContentPath,
            api.SlugPath, api.CurrentPagePath, api.LastPagePath }.Concat(api.Fields.Select(x => x.Path)))
            if (!Regex.IsMatch(path, @"\A[a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)*\z"))
                throw new InvalidOperationException("API paths must use dotted object property names.");
        if (api.SuccessPath.Length > 0 && !Regex.IsMatch(api.SuccessPath, @"\A[a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+)*\z"))
            throw new InvalidOperationException("Invalid API success path.");
        if (!api.Fields.Any(f => f.Path == api.ContentPath && f.Html))
            throw new InvalidOperationException("API fields must include the full content path as HTML/text content.");
        if (api.Fields.Any(f => string.IsNullOrWhiteSpace(f.Label) || f.Label.Length > 80 || f.Label.Contains('\n')))
            throw new InvalidOperationException("Invalid API field label.");
        if ((api.DetailUrlTemplate.Contains("{id}") && !api.ItemUrlTemplate.Contains("{id}"))
            || (api.DetailUrlTemplate.Contains("{slug}") && !api.ItemUrlTemplate.Contains("{slug}")))
            throw new InvalidOperationException("API item URL must contain the detail identity.");
        var first = PageUrl(api, 1);
        if (first.Length > 1500 || PageUrl(api, 2) == first)
            throw new InvalidOperationException("Invalid API page template.");
    }

    private static bool TemplatePathIsSafe(string template)
    {
        var path = template[(template.IndexOf("://", StringComparison.Ordinal) + 3)..];
        return !path.Split('?').Skip(1).Any(x => x.Contains('{'))
            && path.Split('/').Where(x => x.Contains('{')).All(x => x is "{id}" or "{slug}");
    }

    private static Uri CheckUrl(string url, SourceConfiguration conf, string sourceUrl)
    {
        if (url.Contains('{') || url.Contains('}') || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0
            || !(conf.AllowedHosts ?? []).Append(new Uri(sourceUrl).IdnHost).Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("API URLs require public HTTPS on an approved host.");
        return uri;
    }

    public static string PageUrl(JsonApiConfiguration api, int page) =>
        api.ListingUrlTemplate.Replace("{page}", page.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string Expand(string template, string id, string slug) =>
        template.Replace("{id}", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("{slug}", Uri.EscapeDataString(slug), StringComparison.Ordinal);

    public static JsonElement At(JsonElement value, string path)
    {
        foreach (var segment in path.Split('.'))
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return default;
        return value;
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        _ => ""
    };

    private static void Success(JsonElement root, JsonApiConfiguration api)
    {
        if (api.SuccessPath.Length > 0 && At(root, api.SuccessPath).ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("API reported an unsuccessful response; not evidence of closure.");
    }

    public static (List<(string Id, string Url)> Items, bool HasNext) ParsePage(
        JsonElement root, JsonApiConfiguration api, int page)
    {
        Success(root, api);
        var items = At(root, api.ItemsPath);
        var current = At(root, api.CurrentPagePath);
        var last = At(root, api.LastPagePath);
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 250
            || current.ValueKind != JsonValueKind.Number || !current.TryGetInt32(out var n) || n != page
            || last.ValueKind != JsonValueKind.Number || !last.TryGetInt32(out var end) || end < page)
            throw new InvalidOperationException("Unexpected API items or pagination metadata.");
        if (items.GetArrayLength() == 0 && (page != 1 || end != 1))
            throw new InvalidOperationException("API returned an empty intermediate page.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var id = Scalar(At(item, api.IdPath));
            var slug = Scalar(At(item, api.SlugPath));
            if (!SafeIdentity(id) || (UsesSlug(api) && !SafeIdentity(slug)))
                throw new InvalidOperationException("API item identity is missing or invalid.");
            var url = Expand(api.ItemUrlTemplate, id, slug);
            if (url.Length > 1000) throw new InvalidOperationException("API item URL is too long.");
            result[id] = url;
        }
        return (result.Select(x => (x.Key, x.Value)).ToList(), page < end);
    }

    private static bool UsesSlug(JsonApiConfiguration api) =>
        api.ItemUrlTemplate.Contains("{slug}") || api.DetailUrlTemplate.Contains("{slug}");
    private static bool SafeIdentity(string value) =>
        value.Length is > 0 and <= 200 && value is not "." and not ".."
        && Regex.IsMatch(value, @"\A[\p{L}\p{N}_-]+\z");

    public async Task<(List<(string Id, string Url)> Items, bool HasNext)> ReadPageAsync(
        Source source, int page, CancellationToken ct)
    {
        var conf = Configuration(source);
        using var json = await ReadAsync(PageUrl(conf.Api!, page), source, conf, ct);
        var parsed = ParsePage(json.RootElement, conf.Api!, page);
        foreach (var item in parsed.Items) CheckUrl(item.Url, conf, source.Url);
        return parsed;
    }

    public async Task<ContentBundle> ReadDetailAsync(Source source, string itemUrl, CancellationToken ct)
    {
        var conf = Configuration(source);
        var api = conf.Api!;
        CheckUrl(itemUrl, conf, source.Url);
        var pattern = "\\A" + Regex.Escape(api.ItemUrlTemplate)
            .Replace("\\{id}", "(?<id>[\\p{L}\\p{N}_-]+)")
            .Replace("\\{slug}", "(?<slug>[\\p{L}\\p{N}_-]+)") + "\\z";
        var match = Regex.Match(Uri.UnescapeDataString(itemUrl), pattern);
        if (!match.Success) throw new InvalidOperationException("URL does not match the API item template.");
        var id = match.Groups["id"].Value;
        var slug = match.Groups["slug"].Value;
        if ((api.DetailUrlTemplate.Contains("{id}") && id.Length == 0)
            || (api.DetailUrlTemplate.Contains("{slug}") && slug.Length == 0))
            throw new InvalidOperationException("API detail identity cannot be derived from item URL.");
        using var json = await ReadAsync(Expand(api.DetailUrlTemplate, id, slug), source, conf, ct);
        return ParseDetail(json.RootElement, api, itemUrl, id, slug);
    }

    public static ContentBundle ParseDetail(JsonElement root, JsonApiConfiguration api, string itemUrl,
        string expectedId = "", string expectedSlug = "")
    {
        Success(root, api);
        var item = At(root, api.DetailPath);
        if (item.ValueKind != JsonValueKind.Object
            || (expectedId.Length > 0 && Scalar(At(item, api.IdPath)) != expectedId)
            || (expectedSlug.Length > 0 && Scalar(At(item, api.SlugPath)) != expectedSlug))
            throw new InvalidOperationException("API returned a different or missing job detail.");
        if (PlainText(Scalar(At(item, api.ContentPath))).Length < 80)
            throw new ContentNeedsReviewException("API detail content is incomplete; summary is not sufficient for extraction.");
        var lines = new List<string>();
        foreach (var field in api.Fields)
        {
            var value = At(item, field.Path);
            var text = value.ValueKind == JsonValueKind.Array
                ? string.Join("; ", value.EnumerateArray().Select(Scalar).Where(x => x.Length > 0)) : Scalar(value);
            text = field.Html ? PlainText(text) : text;
            if (!string.IsNullOrWhiteSpace(text)) lines.Add(field.Label + ": " + text);
        }
        lines.Add("SOURCE URL: " + itemUrl);
        return new(string.Join('\n', lines), []);
    }

    private static string PlainText(string html)
    {
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var node in doc.QuerySelectorAll("script,style,iframe,form,nav,footer")) node.Remove();
        foreach (var node in doc.QuerySelectorAll("p,div,li,br,h1,h2,h3")) node.Prepend(doc.CreateTextNode("\n"));
        foreach (var node in doc.QuerySelectorAll("a[href]"))
            if (Uri.TryCreate(node.GetAttribute("href"), UriKind.Absolute, out var uri)
                && uri.Scheme == "https" && uri.UserInfo.Length == 0)
                node.Append(doc.CreateTextNode(" " + uri.AbsoluteUri));
        return Regex.Replace(doc.Body?.TextContent ?? "", @"[^\S\n]+", " ").Trim();
    }

    private async Task<JsonDocument> ReadAsync(string url, Source source, SourceConfiguration conf, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(40));
        ct = budget.Token;
        CheckUrl(url, conf, source.Url);
        await policy.ValidateAsync(url, conf.AllowedHosts.Append(new Uri(source.Url).IdnHost), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("HiringPs/1.0 (+https://hiring.ps)");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API returned HTTP {(int)response.StatusCode}; not evidence of closure.", null, response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType is not "application/json")
            throw new InvalidOperationException("Expected JSON from API, not an HTML page.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (data.Length + read > 4 * 1024 * 1024) throw new InvalidOperationException("API response exceeds 4 MB limit.");
            await data.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return JsonDocument.Parse(data.ToArray(), new() { MaxDepth = 32 });
    }

    public void Dispose() { if (ownsClient) http.Dispose(); }
}
