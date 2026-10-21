using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace HiringPs.Web.Models;

/// <summary>
/// Isolates Latin parentheses inside Arabic text, e.g. «متدرب (GS Support Intern)», so the
/// browser keeps the brackets with the English words when the line wraps.
/// </summary>
public static partial class BidiText
{
    [GeneratedRegex(@"\([^()؀-ۿ]*[A-Za-z][^()؀-ۿ]*\)")]
    private static partial Regex LatinGroup();

    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex Arabic();

    public static IHtmlContent Render(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return HtmlString.Empty;
        if (!Arabic().IsMatch(value))
            return new HtmlString(HtmlEncoder.Default.Encode(value));
        var html = new StringBuilder();
        var last = 0;
        foreach (Match match in LatinGroup().Matches(value))
        {
            html.Append(HtmlEncoder.Default.Encode(value[last..match.Index]));
            html.Append("<bdi dir=\"ltr\">")
                .Append(HtmlEncoder.Default.Encode(match.Value))
                .Append("</bdi>");
            last = match.Index + match.Length;
        }
        html.Append(HtmlEncoder.Default.Encode(value[last..]));
        return new HtmlString(html.ToString());
    }

    /// <summary>Direction of a text by its first strong character; falls back to the original language.</summary>
    public static string Direction(string? value, string? language)
    {
        foreach (var c in value ?? "")
        {
            if (c is >= '؀' and <= 'ۿ')
                return "rtl";
            if (char.IsLetter(c))
                return "ltr";
        }
        return language == "ar" ? "rtl" : "ltr";
    }
}
