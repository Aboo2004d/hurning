using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يحتفظ برسالة فشل الأداة وبداية سجل الإطلاق ونهايته ضمن حدود حقول التشخيص.</summary>
public static class ToolFailureDetail
{
    /// <summary>ينسق سلسلة الأسباب دون فقدان نهاية سجل العملية ويخفي كلمات المرور.</summary>
    public static string Describe(Exception error, IConfiguration configuration, int limit = 1800)
    {
        var messages = new List<string>();
        for (Exception? current = error; current is not null; current = current.InnerException)
            messages.Add(current.GetType().Name + ": " + current.Message);
        var text = new ErrorLogSanitizer(configuration).Clean(string.Join("\nCaused by: ", messages));
        if (text.Length <= limit) return text;
        const string marker = "\n[... truncated ...]\n";
        var head = (limit - marker.Length) * 2 / 3;
        return text[..head] + marker + text[^(limit - marker.Length - head)..];
    }

    /// <summary>يضيف فشل العمل الحقيقي إلى نفس حفظ حالته، مع معرف المصدر أو العنصر.</summary>
    public static void Record(HiringDb db, IConfiguration configuration, string context, Exception error)
    {
        db.Audit.Add(OperationTracking.Create(OperationArea.Tools, OperationOutcome.Failed,
            "Execution", context + "; " + Describe(error, configuration)));
    }
}
