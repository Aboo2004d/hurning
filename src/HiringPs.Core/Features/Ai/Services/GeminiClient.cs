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

/// <summary>مكون GeminiClient ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class GeminiClient
{
    public const int DefaultMaxInputCharacters = 120_000;
    public const int DefaultMaxOutputTokens = 32_768;

    /// <summary>ينفذ MaxInputCharacters وفق قواعد GeminiClient الحالية دون تغيير عقد المستدعين.</summary>
    public static int MaxInputCharacters(IConfiguration config) =>
        Math.Clamp(
            config.GetValue<int?>("Ai:MaxInputCharacters") ?? DefaultMaxInputCharacters,
            10_000,
            2_000_000
        );

    /// <summary>ينفذ MaxOutputTokens وفق قواعد GeminiClient الحالية دون تغيير عقد المستدعين.</summary>
    public static int MaxOutputTokens(IConfiguration config) =>
        Math.Clamp(
            config.GetValue<int?>("Ai:MaxOutputTokens") ?? DefaultMaxOutputTokens,
            1_024,
            65_536
        );

    /// <summary>يبني طلب Gemini مع مخطط الاستجابة وحدود الإدخال والإخراج المعتمدة.</summary>
    public static object RequestBody(string instructions, string content, IConfiguration config, bool? useSchema = null)
    {
        var generation = new Dictionary<string, object>
        {
            ["temperature"] = 0,
            ["responseMimeType"] = "application/json",
            ["maxOutputTokens"] = MaxOutputTokens(config),
        };
        if (useSchema ?? (config.GetValue<bool?>("Ai:UseResponseSchema") ?? true))
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

    /// <summary>ينفذ Text وفق قواعد GeminiClient الحالية دون تغيير عقد المستدعين.</summary>
    private static Dictionary<string, object> Text(bool nullable = false) =>
        nullable
            ? new() { ["type"] = "STRING", ["nullable"] = true }
            : new() { ["type"] = "STRING" };

    /// <summary>ينفذ Choice وفق قواعد GeminiClient الحالية دون تغيير عقد المستدعين.</summary>
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
                        ["workMode"] = Choice("On-site", "Remote", "Hybrid"),
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
                                ["propertyOrdering"] = new[] { "kind", "date", "label", "evidence" },
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
                    ["required"] = AiExtractionContract.Fields,
                    ["propertyOrdering"] = AiExtractionContract.Fields,
                },
            },
        },
        ["required"] = new[] { "inScope", "opportunities" },
        ["propertyOrdering"] = new[] { "inScope", "opportunities" },
    };

    static GeminiClient()
    {
        var top = (Dictionary<string, object>)ResponseSchema["properties"];
        var items = (Dictionary<string, object>)((Dictionary<string, object>)top["opportunities"])["items"];
        var fields = (Dictionary<string, object>)items["properties"];
        // Empty enum members are rejected by some Gemini schema implementations.
        // Null represents unknown mode on the wire and is normalized to empty internally.
        ((Dictionary<string, object>)fields["workMode"])["nullable"] = true;
        foreach (var (name, schema) in fields)
            ((Dictionary<string, object>)schema)["description"] = name switch
            {
                "childKey" => "Stable source role reference; do not invent new identifiers on repeated processing.",
                "title" => "Exact original specific role or lot title from the main announcement.",
                "organization" => "Original employer/issuer name, never the recruitment platform.",
                "originalLanguage" => "Language of the original opportunity.",
                "summary" => "Brief overview only. The prompt's summary target applies only here.",
                "locations" => "Actual work/training/tender location, including supported address details. Read explicit location fields. Not headquarters alone.",
                "locationEvidence" => "One exact continuous source quotation establishing this role's actual location.",
                "isInPalestine" => "True for a supported Palestinian location; false for foreign; null when unresolved.",
                "category" => "Infer the actual professional field or subject. Use exactly one canonical catalog label, not an invented alias.",
                "workMode" => "Only an explicitly stated On-site, Remote or Hybrid mode, or null when unknown. Never duties, qualifications, employment type, salary or copied paragraphs. Null for non-job kinds.",
                "employmentType" => "Explicit job contract or full/part-time type only. Empty for non-job kinds.",
                "experienceLevel" => "Concise explicit years/level requirement only. Empty for non-job kinds.",
                "eligibility" => "Explicit applicant restrictions and qualifications. Preserve mandatory conditions here, never in workMode.",
                "referenceNumber" => "Explicit role, tender or lot reference; preserve unchanged.",
                "duration" => "Explicit opportunity/contract duration, not a deadline.",
                "cost" => "Explicit participation, document or course fees only.",
                "applyUrl" => "Explicit HTTPS application link from this announcement, or empty. Never fabricate a fallback.",
                "deadline" => "Explicit application/registration/bid-submission closing date as yyyy-MM-dd, or null. Never opening/publication/course-end dates.",
                "deadlineEvidence" => "Exact continuous source closing clause with its date. Empty when no closing date is indicated.",
                "publicationDate" => "Explicit publication date as yyyy-MM-dd or null; never an application opening date.",
                "publicationEvidence" => "Exact continuous source publication clause or empty.",
                "modificationDate" => "Explicit update date as yyyy-MM-dd or null.",
                "modificationEvidence" => "Exact continuous source update clause or empty.",
                "deadlineIsExtension" => "True only for an explicitly announced extension supported by deadlineEvidence.",
                "otherDates" => "Other explicit dated events; do not duplicate primary dates.",
                "reviewReason" => "Specific unresolved ambiguity only. Unknown optional deadline or applyUrl alone does not require review.",
                _ => "Extract this field independently; do not place another field's facts here.",
            };
    }

    /// <summary>ينفذ RetryAfter وفق قواعد GeminiClient الحالية دون تغيير عقد المستدعين.</summary>
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
    /// <summary>يحلل رد Gemini ويستخرج البيانات ومعلومات الاستخدام وفق العقد الحالي.</summary>
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
            using var extraction = JsonDocument.Parse(text.ToString());
            AiExtractionContract.ValidateJson(extraction.RootElement);
            return JsonSerializer.Deserialize<ExtractionResult>(text.ToString(), AiExtractor.JsonOptions)
                ?? throw new AiExtractionRejectedException("AI returned an empty extraction.");
        }
        catch (JsonException)
        {
            throw new AiExtractionRejectedException(
                "AI extraction contract: invalid JSON, date or enum; return required schema with yyyy-MM-dd dates."
            );
        }
    }
}
