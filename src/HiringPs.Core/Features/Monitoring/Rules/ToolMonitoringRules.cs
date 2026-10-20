using System.ComponentModel.DataAnnotations;

namespace HiringPs.Core;

/// <summary>قواعد قائمة الأدوات وحداثة نتائجها وربطها بنسخة العامل.</summary>
public static class ToolMonitoringRules
{
    /// <summary>يحدد الأدوات التي يشملها نوع طلب الفحص.</summary>
    public static string[] KeysFor(ToolCheckKind kind) => kind switch
    {
        ToolCheckKind.Standard => StandardTools,
        ToolCheckKind.All => ["Collector", .. StandardTools, "AiLive"],
        _ => [kind.ToString()]
    };
    public static readonly string[] StandardTools =
    [
        "AiConfiguration",
        "Chromium",
        "PdfText",
        "PdfRender",
        "PdfOcr",
        "PdfLanguages",
        "PdfWorkspace",
        "PdfPipeline",
        "LahzaPayment",
        "JawwalPayPayment",
        "GoogleLogin",
        "Facebook",
        "LinkedIn",
        "LinkedInPersonal",
    ];

    /// <summary>يتحقق من حداثة نبضة العامل مع السماح بانحراف زمني محدود.</summary>
    public static bool WorkerFresh(ToolWorkerState? worker, DateTimeOffset now) =>
        worker is not null && worker.At >= now.AddSeconds(-60) && worker.At <= now.AddSeconds(30);

    /// <summary>يطابق نتيجة الفحص بنسخة العامل ومدة صلاحيتها.</summary>
    public static bool ResultFresh(
        ToolCheckResult result,
        ToolWorkerState? worker,
        DateTimeOffset now
    ) =>
        WorkerFresh(worker, now)
        && result.WorkerId == worker!.Id
        && result.CheckedAt >= now.AddHours(result.Tool == "AiLive" ? -2 : -26)
        && result.CheckedAt <= now.AddSeconds(30);
}
