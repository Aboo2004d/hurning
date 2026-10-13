using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace HiringPs.Web.Models;

public static class UiMessages
{
    public static string State(string state, IStringLocalizer<Labels> text)
    {
        var value = text["State_" + state];
        return value.ResourceNotFound ? text["UnknownState"].Value : value.Value;
    }
    public static string Connector(string connector, IStringLocalizer<Labels> text) => text["Connector_" + connector].Value;
    public static readonly Dictionary<string, string> Fixed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Local geographic scope needs review."]="Error_Geography",
        ["Location needs review."]="Error_Location",
        ["Original title not found in source text."]="Error_TitleEvidence",
        ["Organization evidence missing."]="Error_OrganizationEvidence",
        ["Reference number evidence missing."]="Error_ReferenceEvidence",
        ["Location evidence missing."]="Error_LocationEvidence",
        ["Deadline evidence missing."]="Error_DeadlineEvidence",
        ["Deadline quotation does not independently confirm the date."]="Error_DeadlineMismatch",
        ["Apply URL not in source text; use original source."]="Error_ApplyEvidence",
        ["Title required (250 maximum)."]="Error_Title",
        ["Organization required (200 maximum)."]="Error_Organization",
        ["Summary must contain 1–200 characters."]="Error_Summary",
        ["Invalid application link."]="Error_ApplyUrl",
        ["Invalid opportunity kind."]="Error_Kind",
        ["Language must be ar or en."]="Error_Language",
        ["Employment fields belong to jobs only."]="Error_JobFields",
        ["AI key is not configured; item remains queued."]="Error_AiKey",
        ["AI quota/credentials unavailable; no paid fallback used."]="Error_AiCredentials",
        ["AI network connection failed; retry scheduled, saved content retained."]="Error_AiNetwork",
        ["Experience level exceeded 60 characters; original AI value is retained in extraction JSON. Enter a short value and approve manually."]="Error_ExperienceLength",
        ["Possible duplicate without a unique reference; administrator review required."]="Error_Duplicate",
        ["Possible repost or similar role across sources; compare and merge in administration if identical."]="Error_Repost",
        ["Role/lot disappeared from revised source; verify before deciding closure."]="Error_RemovedRole",
        ["AI marked out of scope; verify before excluding."]="Error_AiOutOfScope",
        ["Source page timed out before required content became ready; retry scheduled; no source closure inferred."]="Error_PageTimeout",
        ["Source download/request timed out or was cancelled; retry scheduled; no source closure inferred."]="Error_SourceTimeout",
        ["Collection finished; some items are waiting for AI."]="Error_AwaitingAi",
        ["ListingLinkSelector is required."]="Error_Selector",
        ["A listing link selector is required."]="Error_Selector",
        ["Source usage approval and a record of it are required before enabling."]="Error_Approval",
        ["Source must be enabled and usage approved."]="Error_Approval",
        ["ReliefWeb adapter is pending appname approval and verification."]="Error_ReliefApi",
        ["ReliefWeb requires an approved appname and a tested API adapter; this source remains pending."]="Error_ReliefApi",
        ["Invalid public source URL/configuration."]="Error_SourceConfig",
        ["Invalid configuration."]="Error_SourceConfig",
        ["Source URL must use public HTTPS without credentials."]="Error_PublicHttps",
        ["Only public HTTPS URLs without credentials are allowed."]="Error_PublicHttps",
        ["Host is not approved for this source."]="Error_Host",
        ["Private/reserved network targets are not allowed."]="Error_PrivateNetwork",
        ["Invalid connector."]="Error_Connector",
        ["Interval must be 1–168 hours."]="Error_Interval",
        ["MaxPages must be 1–50."]="Error_MaxPages",
        ["Configuration is too large."]="Error_ConfigLength",
        ["Source URL is too long."]="Error_UrlLength",
        ["Source URL must use HTTPS."]="Error_PublicHttps",
        ["A source or application link is required."]="Error_SourceRequired",
        ["Invalid publication status."]="Error_Status",
        ["Closure evidence is required without a deadline."]="Error_Closure",
        ["The record changed; reload before saving."]="Error_Concurrency",
        ["Source changed; reload before saving."]="Error_Concurrency",
        ["RSS description incomplete or too long; source needs review."]="Error_RssContent",
        ["Expected RSS, not a protection page."]="Error_RssFormat",
        ["Expected official RSS channel, not an HTML page."]="Error_RssFormat",
        ["RSS connector requires its official tested feed URL."]="Error_RssUrl",
        ["Job is no longer in the RSS window; not evidence of closure."]="Error_RssWindow",
        ["UNRWA/Inspira requires the official careers.un.org/jobfeed source."]="Error_UnrwaUrl",
        ["UNRWA/Inspira connector requires https://careers.un.org/jobfeed."]="Error_UnrwaUrl",
        ["Incomplete detail content; requires source review."]="Error_Content",
        ["Automatic content discovery is uncertain; source needs review."]="Error_AutoContent",
        ["Automatic discovery found no reliable opportunity links; source needs review."]="Error_AutoLinks",
        ["No listings found; possible protection page or changed source layout."]="Error_AutoLinks",
        ["Interactive pagination detected; source needs a tested adapter before full collection."]="Error_InteractivePages",
        ["Automatic pagination limit reached; source needs review."]="Error_PageLimit",
        ["Pagination limit reached; increase the limit after reviewing this source."]="Error_PageLimit",
        ["Automatic pagination loop; source needs review."]="Error_PageLoop",
        ["Detail exceeds processing limit; configure a precise content selector."]="Error_ContentLimit",
        ["Attachment unavailable; review before publishing."]="Error_Attachment",
        ["PDF is image-only or unreadable; OCR/manual review required."]="Error_PdfImage",
        ["PDF parsing failed; manual review required."]="Error_PdfParse",
        ["Attachment is not a readable PDF."]="Error_PdfParse",
        ["AI returned no opportunity array."]="Error_AiFormat",
        ["Invalid extracted opportunity count."]="Error_AiFormat",
        ["Empty AI result."]="Error_AiFormat",
        ["Missing/duplicate stable child keys; review source."]="Error_AiKeys",
        ["Invalid model name."]="Error_ModelName",
        ["This is an RSS URL; choose its feed connector instead of Automatic HTML discovery."]="Error_RssAutomatic",
        ["Unexpected RSS job link."]="Error_Host",
        ["RSS detail URL changed."]="Error_AutoLinks",
        ["Feed target is not public."]="Error_PrivateNetwork",
        ["More than 3 PDF attachments; configure or review this source."]="Error_AttachmentCount",
        ["Attachment exceeds 8 MB limit."]="Error_AttachmentSize",
        ["PDF exceeds 40 pages; review needed."]="Error_PdfSize",
        ["PDF text exceeds processing limit."]="Error_ContentLimit",
        ["Feed exceeds 4 MB limit."]="Error_RssSize"
    };

    public static string Error(string? message, IStringLocalizer<Labels> text)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        message = Regex.Replace(message, @"\s*Diagnostic ID: [a-fA-F0-9]{32}", "");
        if (message == "LoginFailed") return text[message];
        if (message.StartsWith("Discovery round finished;", StringComparison.Ordinal))
        {
            var counts = Regex.Matches(message, @"(Discovered|AwaitingAi|Retry|Ready|NeedsReview|OutOfScope|Published): (\d+)");
            return text["RoundTotals"].Value + " " + string.Join(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "، " : ", ", counts.Select(m => State(m.Groups[1].Value, text) + ": " + m.Groups[2].Value));
        }
        var rendered = new List<string>(); var remaining = message.Trim();
        while (remaining.Length > 0)
        {
            var entry = Fixed.OrderByDescending(x => x.Key.Length).FirstOrDefault(x => remaining.StartsWith(x.Key, StringComparison.OrdinalIgnoreCase));
            if (entry.Key is not null) { rendered.Add(text[entry.Value]); remaining = remaining[entry.Key.Length..].Trim(); continue; }
            var dynamic = Dynamic(remaining, text);
            rendered.Add(dynamic ?? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? text["Error_Unmapped"].Value : remaining));
            break;
        }
        return string.Join(" ", rendered.Distinct());
    }

    private static string? Dynamic(string message, IStringLocalizer<Labels> t)
    {
        var m = Regex.Match(message, @"^AI field (\w+) exceeds schema limit");
        if (m.Success) return t["Error_FieldLength", Field(m.Groups[1].Value, t)];
        m = Regex.Match(message, @"^AI request timed out after (\d+) seconds");
        if (m.Success) return t["Error_AiTimeout", m.Groups[1].Value];
        m = Regex.Match(message, @"^AI HTTP (\d+): (.+)");
        if (m.Success)
        {
            var code = m.Groups[1].Value;
            var model = Regex.Match(message, @"configured model ([a-zA-Z0-9._-]+)");
            return code switch { "404" or "400" => t["Error_AiModel", code, model.Success ? model.Groups[1].Value : ""], "429" => t["Error_AiQuota", code], "401" or "403" => t["Error_AiAccess", code], _ => t["Error_AiProvider", code] };
        }
        m = Regex.Match(message, @"^(?:Source returned HTTP|Detail HTTP|Source HTTP)\s+(\d+|network error)");
        if (m.Success)
        {
            var wait = Regex.Match(message, @"after waiting (\d+) seconds");
            return wait.Success ? t["Error_HttpWait", m.Groups[1].Value, wait.Groups[1].Value] : t["Error_Http", m.Groups[1].Value];
        }
        if (message.StartsWith("HttpRequestException:", StringComparison.Ordinal)) return t["Error_LegacyNetwork"];
        if (message.StartsWith("TaskCanceledException:", StringComparison.Ordinal)) return t["Error_SourceTimeout"];
        if (message.StartsWith("TimeoutException:", StringComparison.Ordinal)) return t["Error_PageTimeout"];
        if (message.StartsWith("JsonException:", StringComparison.Ordinal) || message.StartsWith("KeyNotFoundException:", StringComparison.Ordinal) || message.StartsWith("IndexOutOfRangeException:", StringComparison.Ordinal)) return t["Error_AiFormat"];
        m = Regex.Match(message, @"^The (.+) field is required\.");
        if (m.Success) return t["Error_Required", Field(m.Groups[1].Value, t)];
        m = Regex.Match(message, @"^The field (.+) must be a string.*maximum length of '(\d+)'");
        if (m.Success) return t["Error_MaxLength", Field(m.Groups[1].Value, t), m.Groups[2].Value];
        return null;
    }
    private static string Field(string name, IStringLocalizer<Labels> t)
    {
        var key = name switch { "ExperienceLevel" => "Experience", "Locations" => "Location", "ReferenceNumber" => "Reference", "EmploymentType" => "Employment", "OriginalLanguage" => "Language", "ApplyUrl" => "ApplyUrl", _ => name };
        var value = t[key]; return value.ResourceNotFound ? t["FieldValue"].Value : value.Value;
    }
}
