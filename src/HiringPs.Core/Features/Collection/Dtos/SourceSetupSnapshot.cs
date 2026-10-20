using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>DTO لعينة صفحة المصدر التي تستخدم لاستخلاص إعدادات موصل قابلة للمراجعة.</summary>
public sealed record SourceSetupSnapshot(
    string Url,
    string Title,
    string Text,
    string Html,
    SourceSetupLink[] Links
);
