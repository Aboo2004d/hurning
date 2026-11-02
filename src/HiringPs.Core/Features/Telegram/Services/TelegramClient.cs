using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>عميل Bot API لإرسال الرسائل وتفسير فشل التسليم دون كشف رمز البوت.</summary>
public sealed class TelegramClient(HttpClient http, IConfiguration configuration)
{
    private string Token => configuration["Telegram:BotToken"] ?? "";
    public string Username => configuration["Telegram:BotUsername"] ?? "";
    public bool Ready =>
        configuration.GetValue<bool>("Telegram:Enabled")
        && Token.Length > 20
        && !Token.Contains('*')
        && Username.Length > 3
        && !Username.Contains('*')
        && configuration["Telegram:WebhookSecret"] is { Length: >= 24 } secret
        && secret.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    /// <summary>ينشئ رابط الخدمة المعروض للعضو دون تغيير حالة حسابه.</summary>
    public string Link(string token) =>
        "https://t.me/" + Uri.EscapeDataString(Username) + "?start=" + token;

    /// <summary>يرسل رسالة Telegram إلى المحادثة المطلوبة ويصنف خطأ التسليم دون تسجيل الرمز السري.</summary>
    public async Task SendAsync(
        long chatId,
        string text,
        CancellationToken ct,
        bool contactButton = false,
        (string Text, string Url)[]? buttons = null,
        string language = "ar",
        (string Text, string Data)[]? callbacks = null
    )
    {
        if (!Ready)
            throw new InvalidOperationException("TelegramUnavailable");
        object? keyboard = contactButton
            ? new
            {
                keyboard = new[]
                {
                    new[]
                    {
                        new
                        {
                            text = language == "en" ? "Share my number" : "مشاركة رقمي",
                            request_contact = true,
                        },
                    },
                },
                resize_keyboard = true,
                one_time_keyboard = true,
            }
            : new { remove_keyboard = true };
        if (buttons is { Length: > 0 })
            keyboard = new
            {
                inline_keyboard = buttons
                    .Select(x => new[] { new { text = x.Text, url = x.Url } })
                    .ToArray(),
            };
        if (callbacks is { Length: > 0 })
        {
            if (callbacks.Length > 20 || callbacks.Any(x => string.IsNullOrWhiteSpace(x.Data)
                || System.Text.Encoding.UTF8.GetByteCount(x.Data) > 64))
                throw new ArgumentException("Invalid callback buttons.", nameof(callbacks));
            var rows = callbacks.Select(x => new object[] { new { text = x.Text, callback_data = x.Data } }).ToList();
            if (buttons is { Length: > 0 })
                rows.AddRange(buttons.Select(x => new object[] { new { text = x.Text, url = x.Url } }));
            keyboard = new { inline_keyboard = rows.ToArray() };
        }
        await CallAsync("sendMessage", new { chat_id = chatId, text, reply_markup = keyboard,
            link_preview_options = new { is_disabled = true } }, ct);
    }

    public async Task AnswerCallbackAsync(string id, CancellationToken ct)
    {
        try { await CallAsync("answerCallbackQuery", new { callback_query_id = id }, ct); }
        // Old buttons remain usable after Telegram's short acknowledgement window has expired.
        catch (TelegramDeliveryException ex) when (ex.Status == 400) { }
    }

    private async Task CallAsync(string method, object payload, CancellationToken ct)
    {
        if (!Ready) throw new InvalidOperationException("TelegramUnavailable");
        HttpResponseMessage result;
        try
        {
            result = await http.PostAsJsonAsync("https://api.telegram.org/bot" + Token + "/" + method, payload, ct);
        }
        catch (HttpRequestException)
        {
            throw new TelegramDeliveryException(503);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TelegramDeliveryException(504);
        }
        using var response = result;
        // Never include the request URI, token or untrusted response body in an exception/log.
        if (!result.IsSuccessStatusCode)
            throw new TelegramDeliveryException((int)result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new TelegramDeliveryException(502);
    }
}
