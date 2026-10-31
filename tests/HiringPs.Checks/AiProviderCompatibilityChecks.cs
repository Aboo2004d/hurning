using System.Net;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;

/// <summary>Exercises schema rejection and compatibility against fixture HTTP, never a live provider.</summary>
internal static class AiProviderCompatibilityChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        const string source = "Administrative assistant at Fixture Org. Work location: Gaza. Responsibilities: maintain accurate approved administrative records and correspondence.";
        var good = new ExtractionResult(true, [new ExtractedOpportunity { ChildKey = "assistant", Kind = OpportunityKind.Job,
            Title = "Administrative assistant", Organization = "Fixture Org", OriginalLanguage = "en", Locations = "Gaza", LocationEvidence = "Work location: Gaza",
            Category = "إدارة", Summary = "Administrative role.", IsInPalestine = true }]);
        var goodJson = JsonSerializer.Serialize(good, AiExtractor.JsonOptions);
        var bad = JsonSerializer.Deserialize<ExtractionResult>(goodJson, AiExtractor.JsonOptions)!;
        bad.Opportunities[0].WorkMode = "On-site / copied detailed duties";
        var badJson = JsonSerializer.Serialize(bad, AiExtractor.JsonOptions);
        IConfiguration Config(string name) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Ai:ApiKey"] = "fixture-api-secret", ["Ai:Model"] = name + "-" + Guid.NewGuid().ToString("N"), ["Ai:SaveRejectedResponses"] = "false"
        }).Build();
        string Error(string message) => JsonSerializer.Serialize(new { error = new { code = 400, status = "INVALID_ARGUMENT", message } });
        string Result(string text) => JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } });
        var schemaError = Error("Invalid response_schema: enum contains an empty string at generation_config.response_schema.");
        var config = Config("schema-compatible");
        using (var handler = new Handler((HttpStatusCode.BadRequest, schemaError), (HttpStatusCode.OK, Result(goodJson))))
        using (var client = new HttpClient(handler))
        {
            var result = await new AiExtractor(client, config).ExtractAsync(source, default);
            Check(result.Opportunities.Count == 1 && handler.SchemaModes.SequenceEqual([true, false]), "schema-specific 400 retries once in compatible JSON mode");
            Check(handler.Instructions.Last().Contains("JSON RESPONSE CONTRACT v2"), "compatible request still explicitly requires all output fields");
        }
        using (var handler = new Handler((HttpStatusCode.OK, Result(goodJson))))
        using (var client = new HttpClient(handler))
        {
            await new AiExtractor(client, config).ExtractAsync(source, default);
            Check(handler.SchemaModes.SequenceEqual([false]), "same account/model avoids repeated known schema rejection");
        }
        using (var handler = new Handler((HttpStatusCode.BadRequest, schemaError), (HttpStatusCode.OK, Result(badJson)), (HttpStatusCode.OK, Result(goodJson))))
        using (var client = new HttpClient(handler))
        {
            var result = await new AiExtractor(client, Config("schema-and-quality")).ExtractAsync(source, default);
            Check(handler.SchemaModes.SequenceEqual([true, false, false]) && result.Opportunities[0].WorkMode == "", "JSON compatibility keeps strict quality checks and one bounded correction");
        }
        using (var handler = new Handler((HttpStatusCode.BadRequest, schemaError), (HttpStatusCode.OK, Result(badJson))))
        using (var client = new HttpClient(handler))
        {
            try { await new AiExtractor(client, Config("schema-bad-quality")).ExtractAsync(source, default); throw new Exception("Expected final quality rejection"); }
            catch (AiExtractionRejectedException) { Check(handler.SchemaModes.Count == 3, "combined schema and quality failures stop after at most three requests"); }
        }
        using (var handler = new Handler((HttpStatusCode.BadRequest, Error("API key fixture-api-secret is not valid."))))
        using (var client = new HttpClient(handler))
        {
            try { await new AiExtractor(client, Config("invalid-key")).ExtractAsync(source, default); throw new Exception("Expected request rejection"); }
            catch (AiUnavailableException ex) { Check(handler.SchemaModes.Count == 1 && ex.Message.Contains("[REDACTED]") && !ex.Message.Contains("fixture-api-secret"), "credential-like 400 is not retried and never exposes the configured key"); }
        }
        using (var handler = new Handler((HttpStatusCode.BadRequest, Error("Invalid maxOutputTokens: requested value exceeds the maximum."))))
        using (var client = new HttpClient(handler))
        {
            try { await new AiExtractor(client, Config("invalid-tokens")).ExtractAsync(source, default); throw new Exception("Expected configuration rejection"); }
            catch (AiUnavailableException ex) { Check(handler.SchemaModes.Count == 1 && ex.Message.Contains("maxOutputTokens"), "non-schema 400 retains the real reason without retrying blindly"); }
        }
        using (var handler = new Handler((HttpStatusCode.TooManyRequests, "{}")))
        using (var client = new HttpClient(handler))
        {
            try { await new AiExtractor(client, Config("quota")).ExtractAsync(source, default); throw new Exception("Expected quota rejection"); }
            catch (AiQuotaException) { Check(handler.SchemaModes.Count == 1, "quota errors never trigger a compatibility fallback"); }
        }
        Check(GeminiProviderError.RejectsSchema("The schema is too complex for serving")
            && !GeminiProviderError.RejectsSchema("Invalid API key for response_schema")
            && !GeminiProviderError.RejectsSchema("Input token context limit exceeded"), "schema detection distinguishes compatibility from access and context errors");
        Check(GeminiProviderError.Message("[]", config).Contains("did not include")
            && GeminiProviderError.Message("{\"error\":\"unexpected\"}", config).Contains("did not include"), "unexpected error envelopes cannot hide the original HTTP failure behind a parser exception");
        Console.WriteLine($"AI provider compatibility: {passed} checks passed without external requests.");
    }
    private sealed class Handler(params (HttpStatusCode Status, string Body)[] replies) : HttpMessageHandler
    {
        public List<bool> SchemaModes { get; } = [];
        public List<string> Instructions { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var reply = replies[Math.Min(SchemaModes.Count, replies.Length - 1)];
            SchemaModes.Add(json.RootElement.GetProperty("generationConfig").TryGetProperty("responseSchema", out _));
            Instructions.Add(json.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!);
            return new HttpResponseMessage(reply.Status) { Content = new StringContent(reply.Body) };
        }
    }
}
