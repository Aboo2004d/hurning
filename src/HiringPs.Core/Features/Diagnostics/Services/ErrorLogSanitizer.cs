using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace HiringPs.Core;

/// <summary>يحجب كلمات المرور فقط مع الحفاظ على النص التشخيصي وروابطه دون اختصار.</summary>
public sealed class ErrorLogSanitizer(IConfiguration configuration)
{
    private const string PasswordName = @"[\w.\[\]-]*(?:password|passwd|passphrase|pwd)[\w.\[\]-]*|كلمة\s*المرور|كلمه\s*المرور";

    /// <summary>يتعرف على حقول كلمة المرور، بما فيها التأكيد والحقول المتداخلة.</summary>
    public static bool IsPasswordField(string? key) => key is not null
        && Regex.IsMatch(key, PasswordName, RegexOptions.IgnoreCase);

    /// <summary>يحجب كلمات المرور المعروفة من الإعدادات والطلب وصيغ JSON/XML والاتصالات دون حذف بقية التفاصيل.</summary>
    public string Clean(string? value, IEnumerable<string>? requestPasswords = null)
    {
        var text = value ?? "";
        var passwords = new HashSet<string>(requestPasswords ?? [], StringComparer.Ordinal);
        foreach (var item in configuration.AsEnumerable())
        {
            if (IsPasswordField(item.Key) && !string.IsNullOrEmpty(item.Value)) passwords.Add(item.Value);
            if (item.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.Value))
            {
                try
                {
                    var password = new SqlConnectionStringBuilder(item.Value).Password;
                    if (!string.IsNullOrEmpty(password)) passwords.Add(password);
                }
                catch (ArgumentException) { /* Non-SQL connection strings still receive textual redaction. */ }
            }
        }
        foreach (var password in passwords.Where(x => !string.IsNullOrEmpty(x)).OrderByDescending(x => x.Length))
        {
            text = text.Replace(password, "[REDACTED]", StringComparison.Ordinal);
            text = text.Replace(Uri.EscapeDataString(password), "[REDACTED]", StringComparison.OrdinalIgnoreCase);
        }
        // Quoted values may contain spaces, semicolons or escaped JSON characters.
        text = Regex.Replace(text,
            "(?<![\\w.\\[\\]-])(?<prefix>(?:[\\\"']?(?:" + PasswordName + ")[\\\"']?)\\s*[=:]\\s*)(?:\\\"(?:\\\\.|[^\\\"\\\\])*\\\"|'(?:''|[^'])*')",
            "${prefix}\"[REDACTED]\"", RegexOptions.IgnoreCase);
        text = Regex.Replace(text,
            "(?<![\\w.\\[\\]-])(?<prefix>(?:[\\\"']?(?:" + PasswordName + ")[\\\"']?)\\s*[=:]\\s*)(?![\\\"'])[^\\s;&<>\\\"']+",
            "${prefix}[REDACTED]", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<open><(?<name>" + PasswordName + @")(?:\s[^>]*)?>)[\s\S]*?(?<close></\k<name>\s*>)",
            "${open}[REDACTED]${close}", RegexOptions.IgnoreCase);
        return text;
    }

    /// <summary>ينظف مجموعات ELMAH الأصلية دون إسقاط الهيدرز أو النماذج أو معلومات الطلب.</summary>
    public void CleanCollection(NameValueCollection values, IEnumerable<string> passwords)
    {
        foreach (var key in values.AllKeys)
        {
            var original = values.GetValues(key) ?? [];
            values.Remove(key);
            foreach (var value in original)
                values.Add(key, IsPasswordField(key) ? "[REDACTED]" : Clean(value, passwords));
        }
    }
}
