using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace HiringPs.Core;

public static class TextEvidence
{
    // Formatting differences only. Never accept a quotation merely because it is semantically similar.
    public static string Normalize(string value) => Regex.Replace(WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormC).Replace('–', '-').Replace('—', '-').Replace('’', '\''), @"\s+", " ").Trim();
    public static bool Contains(string source, string quotation) => !string.IsNullOrWhiteSpace(quotation) && Normalize(source).Contains(Normalize(quotation), StringComparison.OrdinalIgnoreCase);
}
