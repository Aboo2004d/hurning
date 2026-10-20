// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>عميل الاستخراج المنظم من AI مع إدارة المهلة وتصنيف الأخطاء وتتبع الاستهلاك.</summary>
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
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>يطلب استخراجًا من AI ثم يتحقق من الرد المنظم دون افتراض بيانات غائبة.</summary>
    public async Task<ExtractionResult> ExtractAsync(
        string content,
        CancellationToken ct,
        AiExecution? execution = null
    )
    {
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

    /// <summary>يرسل الطلب إلى المزود ويتحقق من الاستجابة والمهلة وفق عقد هذه الخدمة.</summary>
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
        var aiInstructions = prompt.Render();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        );
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(
            new
            {
                systemInstruction = new { parts = new[] { new { text = aiInstructions } } },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = "SOURCE DATA:\n" + content } },
                    },
                },
                generationConfig = new { temperature = 0, responseMimeType = "application/json" },
            }
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
            throw new AiUnavailableException(
                $"AI HTTP {(int)response.StatusCode}: quota/credentials unavailable; no paid fallback used."
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
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        telemetry.Read(result.RootElement);
        var text = result
            .RootElement.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        return JsonSerializer.Deserialize<ExtractionResult>(text ?? "", JsonOptions)
            ?? throw new InvalidOperationException("Empty AI result.");
    }
}
