using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يتذكر مؤقتًا رفض المخطط لنفس الحساب والنموذج والعقد، لتجنب تكرار طلب غير مدعوم لكل عنصر.</summary>
public static class GeminiSchemaCompatibility
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Rejected = new();
    public static bool UseSchema(IConfiguration configuration) => (configuration.GetValue<bool?>("Ai:UseResponseSchema") ?? true)
        && (!Rejected.TryGetValue(Key(configuration), out var until) || until <= DateTimeOffset.UtcNow);
    public static void RememberRejection(IConfiguration configuration) => Rejected[Key(configuration)] = DateTimeOffset.UtcNow.AddMinutes(30);
    private static string Key(IConfiguration configuration) => TextRules.Hash((configuration["Ai:Model"] ?? "") + "\n"
        + (configuration["Ai:ApiKey"] ?? "") + "\n" + JsonSerializer.Serialize(GeminiClient.ResponseSchema));
}
