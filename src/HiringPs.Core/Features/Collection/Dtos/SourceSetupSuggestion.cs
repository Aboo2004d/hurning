using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>DTO لاقتراح إعداد الموصل؛ يراجع قبل حفظه في المصدر.</summary>
public sealed class SourceSetupSuggestion
{
    public bool Supported { get; set; }
    public string Name { get; set; } = "";
    public string ListingUrl { get; set; } = "";
    public string Connector { get; set; } = "";
    public string ListingLinkSelector { get; set; } = "";
    public string ContentSelector { get; set; } = "body";
    public string NextSelector { get; set; } = "";
    public string Explanation { get; set; } = "";
}
