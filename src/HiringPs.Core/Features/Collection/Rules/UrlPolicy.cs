// اكتشاف روابط الفرص وجلب صفحاتها ومرفقاتها، مع فحص النطاقات وتسجيل التشخيص.
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يفرض مخطط HTTPS والمضيف المعتمد ويحظر عناوين الشبكات الخاصة قبل الجمع.</summary>
public class UrlPolicy
{
    /// <summary>يتحقق من HTTPS والمضيف المعتمد وعناوين DNS لمنع الوصول إلى الشبكات الداخلية أو المحجوزة.</summary>
    public async Task<Uri> ValidateAsync(
        string url,
        IEnumerable<string> hosts,
        CancellationToken ct
    )
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
        )
            throw new InvalidOperationException(
                "Only public HTTPS URLs without credentials are allowed."
            );
        if (!hosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Host is not approved for this source.");
        var addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, ct);
        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new InvalidOperationException(
                "Private/reserved network targets are not allowed."
            );
        return uri;
    }

    /// <summary>يكشف عناوين loopback والشبكات الخاصة والمحجوزة قبل السماح بجمع مورد خارجي.</summary>
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return b[0] is 0 or 10 or 127 or >= 224
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 198 && b[1] is 18 or 19);
        return ip.IsIPv6LinkLocal
            || ip.IsIPv6Multicast
            || ip.IsIPv6SiteLocal
            || (b[0] & 0xfe) == 0xfc;
    }
}
