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

/// <summary>مكون AiExtractor ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
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

    /// <summary>يستخرج الفرص من النص مع تطبيق المهلة والقيود وتسجيل الاستخدام.</summary>
    public async Task<ExtractionResult> ExtractAsync(
        string content,
        CancellationToken ct,
        AiExecution? execution = null
    )
    {
        try { return await ExtractAttemptAsync(content, ct, execution, null); }
        catch (AiExtractionRejectedException ex) when ((config.GetValue<int?>("Ai:ExtractionRepairAttempts") ?? 1) > 0 && !ct.IsCancellationRequested)
        {
            if (scopes is not null)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
                var now = DateTimeOffset.UtcNow;
                if (await AiGovernor.PausedUntilAsync(db, now, ct) is not null
                    || await AiGovernor.BudgetExceededAsync(db, config, now, ct)) throw;
            }
            // Exactly one corrective call, separately counted in usage. Never resubmit
            // the previous generated text as source evidence or create an unbounded loop.
            var correction = "EXTRACTION CORRECTION v1: The previous attempt failed validation: " + ex.Reason
                + " Re-read the supplied SOURCE DATA and return every required field independently. "
                + "Use explicit location and date clauses with exact evidence. Infer the canonical category from the actual role. "
                + "Never put duties or qualifications in workMode; use only its enum values. "
                + "Do not repeat one vacancy under different child keys. Unknown facts must remain empty/null; never invent missing facts.";
            return await ExtractAttemptAsync(content, ct, execution, correction);
        }
    }

    /// <summary>ينفذ محاولة واحدة ويسجل استهلاكها ونتيجتها وبصمة التعليمات الفعلية.</summary>
    private async Task<ExtractionResult> ExtractAttemptAsync(string content, CancellationToken ct, AiExecution? execution, string? correction)
    {
        var instructions = (execution?.Version ?? PromptDefaults.Current()).Render()
            + (correction is null ? "" : "\n" + correction);
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
                ct, instructions
            );
        string? error = null;
        try
        {
            var result = await SendAsync(content, instructions, telemetry, ct);
            ExtractionValidator.EnsureShape(result);
            var issues = AiExtractionContract.Issues(result, content);
            if (issues.Count > 0) throw new AiExtractionRejectedException(string.Join(" ", issues));
            result.PromptVersionId = execution?.Version.Id;
            return result;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            if (ex is ContentNeedsReviewException && telemetry.ResponseJson is { } responseJson)
            {
                var diagnostic = await AiResponseDiagnostics.SaveAsync(config, execution, responseJson, ex.Message);
                if (ex is AiExtractionRejectedException rejected) rejected.DiagnosticId = diagnostic;
                if (diagnostic is { } id) logger?.LogWarning("Rejected AI response diagnostic {DiagnosticId}", id.ToString("N"));
            }
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

    /// <summary>يرسل طلب المزود ويصنف نتيجته وفق قواعد الخدمة دون كشف بيانات الاعتماد.</summary>
    private async Task<ExtractionResult> SendAsync(
        string content,
        string instructions,
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
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        );
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(
            GeminiClient.RequestBody(instructions, content, config)
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
        telemetry.ResponseJson = result.RootElement.GetRawText();
        return GeminiClient.ParseResponse(result.RootElement);
    }
}
