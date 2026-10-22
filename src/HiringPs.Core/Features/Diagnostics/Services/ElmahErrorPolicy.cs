using System.Diagnostics;
using ElmahCore;
using Microsoft.AspNetCore.Http;

namespace HiringPs.Core;

/// <summary>يحجب أسرار الاستثناء وبيانات الطلب قبل أن يسجلها ELMAH الأصلي.</summary>
public sealed class ElmahErrorPolicy(ErrorLogSanitizer sanitizer)
{
    /// <summary>يبقي رقم المتابعة والمسار وتوقيت فلسطين فقط مع تفاصيل الاستثناء المنظفة.</summary>
    public void Describe(Error error, HttpContext context)
    {
        error.ServerVariables.Clear();
        error.ServerVariables["TraceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        error.ServerVariables["Path"] = sanitizer.Clean(context.Request.PathBase + context.Request.Path, 2048);
        error.QueryString.Clear();
        error.Form.Clear();
        error.Cookies.Clear();
        error.MessageLog.Clear();
        error.SqlLog.Clear();
        error.Params.Clear();
        error.WebHostHtmlMessage = "";
        error.User = "";
        error.Message = sanitizer.Clean(error.Message, 8000);
        error.Detail = sanitizer.Clean(error.Detail, 64000);
        error.Type = sanitizer.Clean(error.Type, 100);
        error.Source = sanitizer.Clean(error.Source, 60);
        error.HostName = sanitizer.Clean(error.HostName, 50);
        error.Time = error.Time.ToUniversalTime();
        error.ServerVariables["PalestineTime"] = TimeZoneInfo.ConvertTimeFromUtc(error.Time, TextRules.PalestineTimeZone)
            .ToString("yyyy-MM-dd HH:mm:ss") + " — توقيت فلسطين";
    }
}
