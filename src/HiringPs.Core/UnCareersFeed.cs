using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

public static class UnCareersFeed
{
    public const string Url = "https://careers.un.org/jobfeed";
    public static async Task<List<(string Id, string Url)>> DiscoverAsync(UrlPolicy policy, CancellationToken ct)
        => Parse(await ReadXmlAsync(Url, ["careers.un.org"], policy, ct));
    public static async Task<XDocument> ReadXmlAsync(string url, string[] hosts, UrlPolicy policy, CancellationToken ct)
    {
        await policy.ValidateAsync(url, hosts, ct);
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            if (addresses.Length == 0 || addresses.Any(UrlPolicy.IsPrivate)) throw new InvalidOperationException("Feed target is not public.");
            var address = addresses.OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).First();
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HiringPs/1.0 (+https://hiring.ps)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml, text/xml");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var data = new MemoryStream(); var chunk = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (data.Length + read > 4 * 1024 * 1024) throw new InvalidOperationException("Feed exceeds 4 MB limit.");
            await data.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        data.Position = 0;
        using var reader = XmlReader.Create(data, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    public static List<(string Id, string Url)> Parse(XDocument feed)
    {
        if (feed.Root?.Name != "rss" || feed.Root.Element("channel") is not { } channel) throw new InvalidOperationException("Expected official RSS channel, not an HTML page.");
        var results = new Dictionary<string, string>();
        foreach (var item in channel.Elements("item"))
        {
            var description = new HtmlParser().ParseDocument(item.Element("description")?.Value ?? "").Body?.TextContent ?? "";
            var department = Regex.Match(description, @"Department/Office\s*:\s*(.*?)\s*Duty Station\s*:", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
            var station = Regex.Match(description, @"Duty Station\s*:\s*(.*?)\s*(?:Staffing Exercise|Posted Date|Deadline)\s*:", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim();
            if (!department.Contains("UNRWA", StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(station, @"^(?:(?:FO|AREA OFFICE)\s+)?(?:GAZA|WEST BANK|EAST JERUSALEM|JERUSALEM|RAMALLAH|NABLUS|HEBRON)(?:\s+FIELD OFFICE)?$", RegexOptions.IgnoreCase)) continue;
            if (!Uri.TryCreate(item.Element("link")?.Value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IdnHost != "careers.un.org" || !uri.IsDefaultPort || uri.UserInfo.Length > 0) throw new InvalidOperationException("Unexpected RSS detail URL.");
            var match = Regex.Match(uri.AbsolutePath, @"^/jobSearchDescription/(\d+)$");
            if (!match.Success) throw new InvalidOperationException("RSS detail URL changed.");
            var id = match.Groups[1].Value;
            results[id] = $"https://inspira.un.org/psc/UNCAREERS/EMPLOYEE/HRMS/c/UN_CUSTOMIZATIONS.UN_JOB_DETAIL.GBL?JobOpeningId={id}&PostingSeq=1";
        }
        return results.Select(x => (x.Key, x.Value)).ToList();
    }
}
