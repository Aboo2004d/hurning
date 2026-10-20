using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>DTO لنتيجة مساعدة إعداد المصدر واقتراحاتها وملاحظاتها.</summary>
public sealed record SourceSetupResult(
    string Name,
    string Url,
    int Connector,
    string ConfigurationJson,
    string Explanation,
    SourceSetupPreview Preview,
    bool Verified = true
);
