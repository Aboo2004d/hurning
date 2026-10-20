// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>عمليات نصية صغيرة مشتركة لا تعتمد على قاعدة البيانات أو مزود خارجي.</summary>
public static class Strings
{
    /// <summary>يقص النص إلى الحد المطلوب دون تغيير طريقة المعالجة الأخرى.</summary>
    public static string Truncate(this string text, int max) =>
        text.Length <= max ? text : text[..max];
}
