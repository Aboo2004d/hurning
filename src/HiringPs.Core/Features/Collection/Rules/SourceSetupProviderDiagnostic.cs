using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>Inspects a bounded error envelope, returning only sanitized diagnostic fields.</summary>
public static class SourceSetupProviderDiagnostic
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, string apiKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var details = new List<string>
        {
            "Response format: " + (response.Content.Headers.ContentType?.MediaType ?? "unspecified") + "."
        };
        if (GeminiClient.RetryAfter(response) is { TotalSeconds: >= 0 } retry)
            details.Add("Retry-After: " + Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " seconds.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
            using var data = new MemoryStream();
            var chunk = new byte[1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, budget.Token)) > 0)
            {
                if (data.Length + read > 16 * 1024)
                {
                    details.Add("Provider error envelope exceeds the diagnostic size limit.");
                    return string.Join(' ', details);
                }
                await data.WriteAsync(chunk.AsMemory(0, read), budget.Token);
            }
            using var json = JsonDocument.Parse(data.ToArray(), new() { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            {
                details.Add("No structured provider error envelope was returned.");
                return string.Join(' ', details);
            }
            if (error.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                && status.GetString() is { } name && name != apiKey && Regex.IsMatch(name, @"\A[A-Z_]{1,80}\z"))
                details.Add("Provider status: " + name + ".");
            if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                details.Add("Provider message: " + Sanitize(message.GetString() ?? "", apiKey));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            details.Add("Provider error envelope inspection timed out; the original HTTP failure is retained.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or HttpRequestException or InvalidOperationException)
        {
            details.Add("No readable structured provider error envelope was returned.");
        }
        return string.Join(' ', details);
    }

    private static string Sanitize(string message, string apiKey)
    {
        if (apiKey.Length > 0) message = message.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
        message = Regex.Replace(message, @"\bAIza[0-9A-Za-z_-]{20,}\b", "[REDACTED]");
        message = Regex.Replace(message,
            @"(?i)(\b(?:api[_-]?key|x-goog-api-key|access_token|token|password|pwd|client_secret)\s*[=:]\s*)(?:""[^""]*""|'[^']*'|[^\s&;<>]+)",
            "$1[REDACTED]");
        message = Regex.Replace(message, @"(?i)(Bearer\s+)[A-Za-z0-9._~-]+", "$1[REDACTED]");
        message = Regex.Replace(message, @"(https?://)[^/\s:@]+:[^/\s@]+@", "$1[REDACTED]@");
        return Regex.Replace(message, @"\s+", " ").Trim().Truncate(800);
    }
}
