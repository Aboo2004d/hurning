using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يحجب أسرار الإعدادات وأنماط الاعتماد المعروفة قبل تخزين الأخطاء.</summary>
public sealed class ErrorLogSanitizer(IConfiguration configuration)
{
    /// <summary>ينظف النص ويحد حجمه؛ لا تُجمع أجسام الطلبات أو الكوكيز أصلًا.</summary>
    public string Clean(string? value, int limit)
    {
        var text = value ?? "";
        foreach (var item in configuration.AsEnumerable())
            if (!string.IsNullOrEmpty(item.Value) && item.Value.Length >= 4
                && Regex.IsMatch(item.Key, "password|secret|token|apikey|connectionstrings|privatekey", RegexOptions.IgnoreCase))
                text = text.Replace(item.Value, "[REDACTED]", StringComparison.Ordinal);
        text = Regex.Replace(text, @"(?i)((?:bearer|basic)\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        text = Regex.Replace(text, @"(?i)((?:password|pwd|client_secret|access_token|refresh_token|api_key|code|state)[""']?\s*[=:]\s*[""']?)[^\s;&""']+", "$1[REDACTED]");
        text = Regex.Replace(text, @"(?i)/bot[0-9]+:[A-Za-z0-9_-]+", "/bot[REDACTED]");
        text = Regex.Replace(text, @"https?://[^\s<>""']+", match => match.Value.Split('?', '#')[0]);
        return text.Length <= limit ? text : text[..limit];
    }
}
