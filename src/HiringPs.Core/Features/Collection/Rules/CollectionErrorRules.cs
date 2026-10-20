using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحول أخطاء الجمع إلى وصف آمن دون استنتاج إغلاق المصدر.</summary>
public static class CollectionErrorRules
{
    /// <summary>ينشئ رمز خطأ قابلًا للتسجيل دون نص قد يتضمن أسرارًا.</summary>
    public static string SafeError(Exception ex) =>
        ex switch
        {
            CollectorDiagnosticException diagnostic => SafeError(diagnostic.InnerException!)
                + $" Diagnostic ID: {diagnostic.Diagnostic.Id:N}",
            ContentNeedsReviewException => ex.Message.Truncate(1600),
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
