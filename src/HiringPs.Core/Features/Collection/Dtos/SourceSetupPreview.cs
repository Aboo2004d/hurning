using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>DTO لنتيجة تجربة محددات المصدر قبل اعتمادها.</summary>
public sealed record SourceSetupPreview(string[] Links, string? Next, string SampleText);
