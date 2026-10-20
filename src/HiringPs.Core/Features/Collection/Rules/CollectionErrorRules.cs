// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يصنف أخطاء الجمع دون استنتاج إغلاق مصدر من تعذر الوصول إليه.</summary>
public static class CollectionErrorRules
{
    /// <summary>يصنف تعذر الجمع دون تسريب أسرار أو استنتاج انتهاء الإعلان.</summary>
    public static string SafeError(Exception ex) =>
        ex switch
        {
            CollectorDiagnosticException diagnostic => SafeError(diagnostic.InnerException!)
                + $" Diagnostic ID: {diagnostic.Diagnostic.Id:N}",
            ContentNeedsReviewException => ex.Message.Truncate(1600),
            ContentRetryableException => ex.Message.Truncate(1600),
            InvalidOperationException => ex.Message.Truncate(1800),
            HttpRequestException http =>
                $"Source HTTP {(http.StatusCode is { } code ? ((int)code).ToString() : "network error")}; retry scheduled; no source closure inferred.",
            TimeoutException =>
                "Source page timed out before required content became ready; retry scheduled; no source closure inferred.",
            OperationCanceledException =>
                "Source download/request timed out or was cancelled; retry scheduled; no source closure inferred.",
            _ => $"{ex.GetType().Name}: inspect server logs; no source closure inferred.",
        };
}
