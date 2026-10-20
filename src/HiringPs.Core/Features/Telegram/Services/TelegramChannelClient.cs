// النشر التلقائي في قناة تليجرام العامة: الإعدادات، العميل، وطابور التسليم فوق سجل النشر الاجتماعي.
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون TelegramChannelClient ضمن مجال Telegram؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed class TelegramChannelClient(HttpClient http, IConfiguration configuration)
{
    private string Token => configuration["Telegram:BotToken"] ?? "";
    public bool Ready => Token.Length > 20 && !Token.Contains('*');

    /// <summary>يرسل طلب المزود ويصنف نتيجته وفق قواعد الخدمة دون كشف بيانات الاعتماد.</summary>
    public async Task<TelegramChannelResult> SendAsync(
        string chat,
        string html,
        string? previewUrl,
        CancellationToken ct
    )
    {
        if (!Ready)
            return new(TelegramChannelOutcome.Rejected, "TelegramBotNotConfigured");
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                "https://api.telegram.org/bot" + Token + "/sendMessage",
                new
                {
                    chat_id = chat,
                    text = html,
                    parse_mode = "HTML",
                    // Previews stay enabled and point at the canonical opportunity link.
                    link_preview_options = new { is_disabled = false, url = previewUrl },
                },
                ct
            );
        }
        catch (HttpRequestException ex)
            when (ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError)
        {
            // The request never reached Telegram: safe to retry.
            return new(TelegramChannelOutcome.RetryLater, "TelegramApiUnavailable", 60);
        }
        catch (HttpRequestException)
        {
            return new(TelegramChannelOutcome.Ambiguous, "TelegramDeliveryUnknown");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(TelegramChannelOutcome.Ambiguous, "TelegramDeliveryUnknown");
        }
        using (response)
        {
            var status = (int)response.StatusCode;
            JsonDocument? json = null;
            try
            {
                json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (JsonException) { }
            using (json)
            {
                var root = json?.RootElement;
                if (
                    response.IsSuccessStatusCode
                    && root is { } ok
                    && ok.TryGetProperty("ok", out var okValue)
                    && okValue.ValueKind == JsonValueKind.True
                    && ok.TryGetProperty("result", out var message)
                )
                {
                    var id = message.TryGetProperty("message_id", out var mid) ? mid.GetRawText() : "";
                    var username =
                        message.TryGetProperty("chat", out var chatInfo)
                        && chatInfo.TryGetProperty("username", out var name)
                            ? name.GetString() ?? ""
                            : "";
                    return new(TelegramChannelOutcome.Sent, "", null, id, MessageUrl(chat, username, id));
                }
                var description =
                    root is { } error && error.TryGetProperty("description", out var text)
                        ? text.GetString() ?? ""
                        : "";
                if (status == 429)
                {
                    var retry =
                        root is { } limited
                        && limited.TryGetProperty("parameters", out var parameters)
                        && parameters.TryGetProperty("retry_after", out var after)
                        && after.TryGetInt32(out var seconds)
                            ? seconds
                            : 60;
                    return new(TelegramChannelOutcome.RetryLater, "TelegramRateLimited", Math.Clamp(retry, 1, 86400));
                }
                if (status == 401 || status == 404)
                    return new(TelegramChannelOutcome.Rejected, "TelegramTokenInvalid");
                if (status == 403 || description.Contains("not enough rights", StringComparison.OrdinalIgnoreCase))
                    return new(TelegramChannelOutcome.Rejected, "TelegramChannelForbidden");
                if (description.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
                    return new(TelegramChannelOutcome.Rejected, "TelegramChannelNotFound");
                if (status == 400)
                    return new(TelegramChannelOutcome.Rejected, "TelegramMessageInvalid");
                if (status >= 500)
                    return new(TelegramChannelOutcome.RetryLater, "TelegramApiUnavailable", 120);
                return new(TelegramChannelOutcome.Rejected, "TelegramChannelRejected");
            }
        }
    }

    /// <summary>ينفذ MessageUrl وفق قواعد TelegramChannelClient الحالية دون تغيير عقد المستدعين.</summary>
    private static string MessageUrl(string chat, string username, string messageId)
    {
        if (messageId.Length == 0)
            return "";
        if (username.Length > 0)
            return "https://t.me/" + Uri.EscapeDataString(username) + "/" + messageId;
        if (chat.StartsWith('@'))
            return "https://t.me/" + Uri.EscapeDataString(chat[1..]) + "/" + messageId;
        return chat.StartsWith("-100", StringComparison.Ordinal)
            ? "https://t.me/c/" + chat[4..] + "/" + messageId
            : "";
    }
}
