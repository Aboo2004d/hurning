using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يحفظ الرد الأصلي المرفوض خارج wwwroot دون رؤوس HTTP أو مفاتيح المزود.</summary>
public static class AiResponseDiagnostics
{
    /// <summary>يحفظ تشخيصًا محدودًا، ولا يجعل تعذر الكتابة سببًا لفقدان الخطأ الأصلي.</summary>
    public static async Task<Guid?> SaveAsync(IConfiguration configuration, AiExecution? execution, string response, string reason)
    {
        if (configuration.GetValue<bool?>("Ai:SaveRejectedResponses") == false || response.Length > 2_000_000) return null;
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var id = Guid.NewGuid();
            var directory = Path.Combine(CollectorDiagnosticFiles.DirectoryPath(configuration), "ai-responses");
            Directory.CreateDirectory(directory);
            var data = JsonSerializer.Serialize(new { id, at = DateTimeOffset.UtcNow, execution?.SourceItemId,
                promptVersionId = execution?.Version.Id, model = configuration["Ai:Model"],
                schemaEnabled = configuration.GetValue<bool?>("Ai:UseResponseSchema") ?? true,
                schemaHash = TextRules.Hash(JsonSerializer.Serialize(GeminiClient.ResponseSchema)), reason,
                providerResponse = JsonSerializer.Deserialize<JsonElement>(response) }, CollectorDiagnosticFiles.Json);
            await File.WriteAllTextAsync(Path.Combine(directory, id.ToString("N") + ".json"), data, budget.Token);
            return id;
        }
        catch (Exception) { return null; }
    }
}
