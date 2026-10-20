// طلب Gemini وقراءة ردّه: حدود المدخلات والمخرجات، مخطط JSON، وتصنيف أخطاء المزوّد.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public record ExtractionResult(bool InScope, List<ExtractedOpportunity> Opportunities)
{
    [JsonIgnore]
    public int? PromptVersionId { get; set; }
}

public class ExtractedOpportunity
{
    public string ChildKey { get; set; } = "";
    public OpportunityKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
    public string OriginalLanguage { get; set; } = "ar";
    public string Summary { get; set; } = "";
    public string Locations { get; set; } = "";
    public string Category { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string WorkMode { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Eligibility { get; set; } = "";
    public string Cost { get; set; } = "";
    public string ApplyUrl { get; set; } = "";
    public DateOnly? Deadline { get; set; }
    public string DeadlineEvidence { get; set; } = "";
    public DateOnly? PublicationDate { get; set; }
    public string PublicationEvidence { get; set; } = "";
    public DateOnly? ModificationDate { get; set; }
    public string ModificationEvidence { get; set; } = "";
    public bool DeadlineIsExtension { get; set; }
    public List<ExtractedDate> OtherDates { get; set; } = [];
    public string LocationEvidence { get; set; } = "";
    public bool? IsInPalestine { get; set; }
    public string ReviewReason { get; set; } = "";
}

public class AiUnavailableException(string message) : Exception(message);

// Quota (429) or credential (401/403) refusals pause every AI call, not just one item.
public class AiQuotaException(string message, TimeSpan? retryAfter, bool credentials)
    : AiUnavailableException(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool Credentials { get; } = credentials;
}

public class AiTransientException(string message) : Exception(message);

public static class GeminiClient
{
    public const int DefaultMaxInputCharacters = 120_000;
    public const int DefaultMaxOutputTokens = 32_768;

    public static int MaxInputCharacters(IConfiguration config) =>
        Math.Clamp(
            config.GetValue<int?>("Ai:MaxInputCharacters") ?? DefaultMaxInputCharacters,
            10_000,
            2_000_000
        );

    public static int MaxOutputTokens(IConfiguration config) =>
        Math.Clamp(
            config.GetValue<int?>("Ai:MaxOutputTokens") ?? DefaultMaxOutputTokens,
            1_024,
            65_536
        );

    public static object RequestBody(string instructions, string content, IConfiguration config)
    {
        var generation = new Dictionary<string, object>
        {
            ["temperature"] = 0,
            ["responseMimeType"] = "application/json",
            ["maxOutputTokens"] = MaxOutputTokens(config),
        };
        if (config.GetValue<bool?>("Ai:UseResponseSchema") ?? true)
            generation["responseSchema"] = ResponseSchema;
        return new
        {
            systemInstruction = new { parts = new[] { new { text = instructions } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = "SOURCE DATA:\n" + content } } },
            },
            generationConfig = generation,
        };
    }

    private static Dictionary<string, object> Text(bool nullable = false) =>
        nullable
            ? new() { ["type"] = "STRING", ["nullable"] = true }
            : new() { ["type"] = "STRING" };

    private static Dictionary<string, object> Choice(params string[] values) =>
        new()
        {
            ["type"] = "STRING",
            ["format"] = "enum",
            ["enum"] = values,
        };

    // Mirrors ExtractedOpportunity; dates stay strings (yyyy-MM-dd) and are validated afterwards.
    public static readonly Dictionary<string, object> ResponseSchema = new()
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["inScope"] = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
            ["opportunities"] = new Dictionary<string, object>
            {
                ["type"] = "ARRAY",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["childKey"] = Text(),
                        ["kind"] = Choice(Enum.GetNames<OpportunityKind>()),
                        ["title"] = Text(),
                        ["organization"] = Text(),
                        ["originalLanguage"] = Choice("ar", "en"),
                        ["summary"] = Text(),
                        ["locations"] = Text(),
                        ["category"] = Text(),
                        ["employmentType"] = Text(),
                        ["experienceLevel"] = Text(),
                        ["workMode"] = Text(),
                        ["referenceNumber"] = Text(),
                        ["duration"] = Text(),
                        ["eligibility"] = Text(),
                        ["cost"] = Text(),
                        ["applyUrl"] = Text(),
                        ["deadline"] = Text(nullable: true),
                        ["deadlineEvidence"] = Text(),
                        ["publicationDate"] = Text(nullable: true),
                        ["publicationEvidence"] = Text(),
                        ["modificationDate"] = Text(nullable: true),
                        ["modificationEvidence"] = Text(),
                        ["deadlineIsExtension"] = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
                        ["otherDates"] = new Dictionary<string, object>
                        {
                            ["type"] = "ARRAY",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "OBJECT",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["kind"] = Choice(Enum.GetNames<OpportunityDateKind>()),
                                    ["date"] = Text(),
                                    ["label"] = Text(),
                                    ["evidence"] = Text(),
                                },
                                ["required"] = new[] { "kind", "date", "label", "evidence" },
                            },
                        },
                        ["locationEvidence"] = Text(),
                        ["isInPalestine"] = new Dictionary<string, object>
                        {
                            ["type"] = "BOOLEAN",
                            ["nullable"] = true,
                        },
                        ["reviewReason"] = Text(),
                    },
                    ["required"] = new[] { "childKey", "kind", "title", "organization" },
                },
            },
        },
        ["required"] = new[] { "inScope", "opportunities" },
    };

    public static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
            return delta;
        if (header?.Date is { } date)
            return date - DateTimeOffset.UtcNow;
        return null;
    }

    // Truncated, blocked or empty answers are content problems: another identical call would bill again.
    public static ExtractionResult ParseResponse(JsonElement root)
    {
        if (
            !root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0
        )
        {
            var blocked =
                root.TryGetProperty("promptFeedback", out var feedback)
                && feedback.ValueKind == JsonValueKind.Object
                && feedback.TryGetProperty("blockReason", out var reason)
                    ? reason.GetString()
                    : null;
            throw new ContentNeedsReviewException(
                blocked is null
                    ? "AI returned no answer candidates; original text retained for manual review."
                    : $"AI refused the input ({blocked.Truncate(40)}); original text retained for manual review."
            );
        }
        var first = candidates[0];
        var finish =
            first.TryGetProperty("finishReason", out var finishValue)
            && finishValue.ValueKind == JsonValueKind.String
                ? finishValue.GetString() ?? ""
                : "";
        if (finish == "MAX_TOKENS")
            throw new ContentNeedsReviewException(
                "AI output reached the output token limit and was truncated; review manually or raise Ai:MaxOutputTokens."
            );
        if (finish is not ("" or "STOP" or "FINISH_REASON_UNSPECIFIED"))
            throw new ContentNeedsReviewException(
                $"AI stopped with {finish.Truncate(40)}; original text retained for manual review."
            );
        var text = new StringBuilder();
        if (
            first.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Object
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array
        )
            foreach (var part in parts.EnumerateArray())
                if (
                    part.ValueKind == JsonValueKind.Object
                    && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                    && part.TryGetProperty("text", out var value)
                    && value.ValueKind == JsonValueKind.String
                )
                    text.Append(value.GetString());
        if (text.Length == 0)
            throw new ContentNeedsReviewException(
                "AI answer has no text content; original text retained for manual review."
            );
        try
        {
            return JsonSerializer.Deserialize<ExtractionResult>(text.ToString(), AiExtractor.JsonOptions)
                ?? throw new ContentNeedsReviewException("AI returned an empty extraction.");
        }
        catch (JsonException)
        {
            throw new ContentNeedsReviewException(
                "AI output does not match the extraction contract (invalid JSON, date or enum); review manually."
            );
        }
    }
}

public class AiExtractor
{
    private readonly HttpClient client;
    private readonly IConfiguration config;
    private readonly IServiceScopeFactory? scopes;
    private readonly ILogger<AiExtractor>? logger;

    public AiExtractor(
        HttpClient client,
        IConfiguration config,
        IServiceScopeFactory? scopes = null,
        ILogger<AiExtractor>? logger = null
    )
    {
        this.client = client;
        this.config = config;
        this.scopes = scopes;
        this.logger = logger;
        client.Timeout = TimeSpan.FromSeconds(
            Math.Clamp(config.GetValue<int?>("Ai:TimeoutSeconds") ?? 180, 30, 300)
        );
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new OptionalDateConverter() },
    };

    // An empty string for an unknown optional date means null, not a contract violation.
    private sealed class OptionalDateConverter : JsonConverter<DateOnly?>
    {
        public override DateOnly? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Optional date must be a string.");
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return DateOnly.TryParseExact(
                text.Trim(),
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date
            )
                ? date
                : throw new JsonException("Optional date must be yyyy-MM-dd.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateOnly? value,
            JsonSerializerOptions options
        )
        {
            if (value is { } date)
                writer.WriteStringValue(
                    date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                );
            else
                writer.WriteNullValue();
        }
    }

    public async Task<ExtractionResult> ExtractAsync(
        string content,
        CancellationToken ct,
        AiExecution? execution = null
    )
    {
        var limit = GeminiClient.MaxInputCharacters(config);
        if (content.Length > limit)
            throw new ContentNeedsReviewException(
                $"Source text has {content.Length} characters, above the AI input limit of {limit} (Ai:MaxInputCharacters); original text retained for manual review."
            );
        var telemetry = new AiTelemetry();
        long? usageId = null;
        var started = System.Diagnostics.Stopwatch.StartNew();
        if (scopes is not null && execution is not null)
            usageId = await AiUsageRecorder.StartAsync(
                scopes,
                execution,
                config["Ai:Model"] ?? "",
                ct
            );
        string? error = null;
        try
        {
            var result = await SendAsync(content, execution, telemetry, ct);
            result.PromptVersionId = execution?.Version.Id;
            return result;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            throw;
        }
        finally
        {
            if (usageId is { } id && scopes is not null)
            {
                try
                {
                    await AiUsageRecorder.FinishAsync(
                        scopes,
                        id,
                        telemetry,
                        error,
                        started.ElapsedMilliseconds
                    );
                }
                catch (Exception)
                {
                    logger?.LogWarning(
                        "AI usage record {Id} remains unfinished; result was not discarded.",
                        id
                    );
                }
            }
        }
    }

    private async Task<ExtractionResult> SendAsync(
        string content,
        AiExecution? execution,
        AiTelemetry telemetry,
        CancellationToken ct
    )
    {
        var key = config["Ai:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key == "***")
            throw new AiUnavailableException("AI key is not configured; item remains queued.");
        var model = config["Ai:Model"];
        if (string.IsNullOrWhiteSpace(model))
            throw new AiUnavailableException("AI model is not configured; item remains queued.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(model, @"\A[a-zA-Z0-9._-]+\z"))
            throw new AiUnavailableException("AI model is invalid; item remains queued.");
        var prompt = execution?.Version ?? PromptDefaults.Initial();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        );
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(
            GeminiClient.RequestBody(prompt.Render(), content, config)
        );
        HttpResponseMessage received;
        try
        {
            received = await client.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiTransientException(
                $"AI request timed out after {client.Timeout.TotalSeconds:0} seconds; retry scheduled, saved content retained."
            );
        }
        catch (HttpRequestException)
        {
            throw new AiTransientException(
                "AI network connection failed; retry scheduled, saved content retained."
            );
        }

        using var response = received;
        telemetry.HttpStatus = (int)response.StatusCode;
        if (
            response.StatusCode
            is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.RequestTimeout
        )
            throw new AiTransientException(
                $"AI HTTP {(int)response.StatusCode}: temporary provider error; retry scheduled, saved content retained."
            );
        if (
            response.StatusCode == HttpStatusCode.TooManyRequests
            || response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden
        )
            throw new AiQuotaException(
                $"AI HTTP {(int)response.StatusCode}: quota/credentials unavailable; no paid fallback used.",
                GeminiClient.RetryAfter(response),
                response.StatusCode != HttpStatusCode.TooManyRequests
            );
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Inspect only for a limit classification; never store the provider body or echoed source/key.
            var body = await response.Content.ReadAsStringAsync(ct);
            if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    body,
                    @"(?is)(?:input token|context length|context window).{0,160}(?:exceed|maximum|limit)|(?:exceed|too many).{0,120}(?:input token|context length)"
                )
            )
                throw new ContentNeedsReviewException(
                    "AI input exceeds the provider context limit; original text is saved. Use a model with a larger supported context or review manually."
                );
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            throw new AiUnavailableException(
                $"AI HTTP {(int)response.StatusCode}: check configured model {model} and request compatibility."
            );
        response.EnsureSuccessStatusCode();
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (JsonException)
        {
            // A malformed provider envelope is not the extraction itself; retry with backoff.
            throw new AiTransientException("AI provider returned a malformed response; retry scheduled.");
        }
        using var result = parsed;
        telemetry.Read(result.RootElement);
        return GeminiClient.ParseResponse(result.RootElement);
    }
}
