using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>حقائق استهلاك وتشخيص استدعاء AI داخل الذاكرة قبل حفظ سجل الاستخدام.</summary>
public class AiTelemetry
{
    public int? HttpStatus { get; set; }
    public int? Input { get; set; }
    public int? Output { get; set; }
    public int? Cached { get; set; }
    public int? Total { get; set; }

    public void Read(JsonElement response)
    {
        if (
            !response.TryGetProperty("usageMetadata", out var usage)
            || usage.ValueKind != JsonValueKind.Object
        )
            return;
        int? ReadCount(string key) =>
            usage.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var count)
            && count >= 0
                ? count
                : null;
        Input = ReadCount("promptTokenCount");
        Output = ReadCount("candidatesTokenCount");
        Cached = ReadCount("cachedContentTokenCount");
        Total = ReadCount("totalTokenCount");
    }
}
