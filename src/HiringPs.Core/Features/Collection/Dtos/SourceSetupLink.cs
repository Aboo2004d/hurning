using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>DTO لرابط ظهر في الصفحة والنص المرافق له.</summary>
public sealed record SourceSetupLink(string Url, string Text);
