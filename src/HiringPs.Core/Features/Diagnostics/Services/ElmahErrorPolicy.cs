using System.Diagnostics;
using System.Security.Claims;
using System.Collections.Specialized;
using ElmahCore;
using Microsoft.AspNetCore.Http;

namespace HiringPs.Core;

/// <summary>يحفظ تفاصيل ELMAH وهوية المستخدم كاملة مع حجب كلمات المرور فقط.</summary>
public sealed class ElmahErrorPolicy(ErrorLogSanitizer sanitizer)
{
    /// <summary>يحتفظ بسلسلة الاستثناء وموقعه وبيانات الطلب، ويضيف المستخدم ورقم المتابعة وتوقيت فلسطين.</summary>
    public void Describe(Error error, HttpContext context)
    {
        var collections = new[] { error.QueryString, error.Form, error.Cookies, error.ServerVariables };
        var passwords = collections.SelectMany(values => values.AllKeys
                .Where(ErrorLogSanitizer.IsPasswordField)
                .SelectMany(key => values.GetValues(key) ?? []))
            .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var values in collections) sanitizer.CleanCollection(values, passwords);
        error.User = sanitizer.Clean(context.User.Identity?.Name ?? "", passwords);
        error.ServerVariables["UserId"] = sanitizer.Clean(context.User.FindFirstValue(ClaimTypes.NameIdentifier), passwords);
        error.ServerVariables["UserEmail"] = sanitizer.Clean(context.User.FindFirstValue(ClaimTypes.Email), passwords);
        error.ServerVariables["UserRoles"] = sanitizer.Clean(string.Join(", ", context.User.FindAll(ClaimTypes.Role).Select(x => x.Value)), passwords);
        error.ServerVariables["IsAuthenticated"] = (context.User.Identity?.IsAuthenticated == true).ToString();
        error.ServerVariables["TraceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        error.ServerVariables["Path"] = sanitizer.Clean(context.Request.PathBase + context.Request.Path, passwords);
        error.Message = sanitizer.Clean(error.Message, passwords);
        error.Detail = sanitizer.Clean(error.Detail, passwords);
        error.WebHostHtmlMessage = sanitizer.Clean(error.WebHostHtmlMessage, passwords);
        // Native SQL indexed columns have fixed widths; full names remain available in the XML metadata.
        error.ServerVariables["ExceptionType"] = sanitizer.Clean(error.Type, passwords);
        error.ServerVariables["ExceptionSource"] = sanitizer.Clean(error.Source, passwords);
        error.ServerVariables["UserName"] = error.User;
        error.Type = sanitizer.Clean(error.Type, passwords).Truncate(100);
        error.Source = sanitizer.Clean(error.Source, passwords).Truncate(60);
        error.HostName = sanitizer.Clean(error.HostName, passwords).Truncate(50);
        error.User = error.User.Truncate(50);
        for (var i = 0; i < error.MessageLog.Count; i++)
        {
            var message = error.MessageLog[i];
            message.Message = sanitizer.Clean(message.Message, passwords);
            message.Exception = sanitizer.Clean(message.Exception, passwords);
            message.Scope = sanitizer.Clean(message.Scope, passwords);
            if (message.Params is not null)
                message.Params = CleanParams(message.Params, passwords);
            error.MessageLog[i] = message;
        }
        foreach (var sql in error.SqlLog) sql.SqlText = sanitizer.Clean(sql.SqlText, passwords);
        for (var i = 0; i < error.Params.Count; i++)
        {
            var entry = error.Params[i];
            error.Params[i] = new(entry.TimeStamp, CleanParams(entry.Params, passwords), entry.TypeName,
                entry.MemberName, entry.File, entry.Line);
        }
        error.Time = error.Time.ToUniversalTime();
        error.ServerVariables["PalestineTime"] = TimeZoneInfo.ConvertTimeFromUtc(error.Time, TextRules.PalestineTimeZone)
            .ToString("yyyy-MM-dd HH:mm:ss") + " — توقيت فلسطين";
    }

    private KeyValuePair<string, string>[] CleanParams(KeyValuePair<string, string>[] values, string[] passwords) =>
        values.Select(pair => new KeyValuePair<string, string>(pair.Key,
            ErrorLogSanitizer.IsPasswordField(pair.Key) ? "[REDACTED]" : sanitizer.Clean(pair.Value, passwords))).ToArray();
}
