// إبلاغ محركات البحث عبر IndexNow بتغير صفحات الفرص العامة، على دفعات من العامل وليس داخل طلبات الويب.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون PublicUrls ضمن مجال Indexing؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class PublicUrls
{
    /// <summary>يتحقق من الأصل العام HTTPS دون مسارات أو بيانات مستخدم.</summary>
    public static string Origin(IConfiguration configuration)
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
            throw new InvalidOperationException("PublicOriginRequired");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>ينفذ Details وفق قواعد PublicUrls الحالية دون تغيير عقد المستدعين.</summary>
    public static string Details(string origin, long id, string language) =>
        origin
        + (language == "en" ? "/en" : "/ar")
        + "/Opportunities/Details/"
        + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>ينفذ Listing وفق قواعد PublicUrls الحالية دون تغيير عقد المستدعين.</summary>
    public static string Listing(string origin, string language, OpportunityKind kind) =>
        origin + (language == "en" ? "/en" : "/ar") + "/?kind=" + kind;

    /// <summary>ينفذ WithSource وفق قواعد PublicUrls الحالية دون تغيير عقد المستدعين.</summary>
    public static string WithSource(string url, string source) =>
        url + (url.Contains('?') ? "&" : "?") + "utm_source=" + Uri.EscapeDataString(source);
}

// One row per opportunity: both language URLs are submitted together and share the record.
