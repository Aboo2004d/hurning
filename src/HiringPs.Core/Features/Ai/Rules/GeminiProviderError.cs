using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يقرأ سبب رفض المزود ويحجب المفتاح؛ لا يخلط رفض المخطط بأخطاء الاعتماد أو حدود السياق.</summary>
public static class GeminiProviderError
{
    public static string Message(string body, IConfiguration configuration)
    {
        string message;
        try
        {
            using var json = JsonDocument.Parse(body);
            message = json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String
                ? text.GetString() ?? "" : "Provider did not include an error message.";
        }
        catch (JsonException) { message = "Provider returned a non-JSON error response."; }
        return Regex.Replace(Sanitize(message, configuration), @"\s+", " ").Trim().Truncate(1100);
    }

    public static string Sanitize(string value, IConfiguration configuration) =>
        new ErrorLogSanitizer(configuration).Clean(value, [configuration["Ai:ApiKey"] ?? ""]);

    /// <summary>يتطلب ذكر المخطط أو حقل منه مع صيغة رفض؛ أخطاء المفاتيح والنموذج لا تفعّل التوافق.</summary>
    public static bool RejectsSchema(string message) =>
        !Regex.IsMatch(message, @"API.?key|credential|permission|authentication|context|input.?token|max.?output.?token", RegexOptions.IgnoreCase)
        && Regex.IsMatch(message, @"response[_ ]?schema|responseJsonSchema|propertyOrdering|\bschema\b|structured\s+(?:output|response)|controlled\s+generation|generation[_ .]?config[^.]{0,30}schema", RegexOptions.IgnoreCase)
        && Regex.IsMatch(message, @"invalid|unsupported|not supported|unknown|too (?:large|complex|many)|exceed|empty|must|cannot|not allowed", RegexOptions.IgnoreCase);
}
