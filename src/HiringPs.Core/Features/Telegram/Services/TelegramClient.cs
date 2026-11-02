using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>عميل Bot API لإرسال الرسائل وتفسير فشل التسليم دون كشف رمز البوت.</summary>
public sealed class TelegramClient(HttpClient http, IConfiguration configuration)
{
    private ReplyScope? reply;

    // A scope belongs to one validated private update, never to a shared user/session cache.
    public async Task<IAsyncDisposable> BeginReplyAsync(long chat, long? message, CancellationToken ct)
    {
        var scope = new ReplyScope(this, chat, message);
        reply = scope;
        try
        {
            await SendAsync(chat, "⏳ جار جلب النتائج… / Fetching results…", ct);
            scope.Waiting = true;
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }

    private sealed class ReplyScope(TelegramClient owner, long chat, long? message) : IAsyncDisposable
    {
        public long Chat { get; } = chat;
        public long? Message { get; set; } = message;
        public bool Waiting { get; set; }
        public void Dispose() { if (ReferenceEquals(owner.reply, this)) owner.reply = null; }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Waiting)
                {
                    // The worker/shutdown token may already be cancelled. Bound best-effort cleanup independently.
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try { await owner.SendAsync(Chat, "تعذّر إكمال الطلب. حاول مجددًا. / Request interrupted. Please try again.", cleanup.Token); }
                    catch (Exception ex) when (ex is TelegramDeliveryException or OperationCanceledException or HttpRequestException) { }
                }
            }
            finally { Dispose(); }
        }
    }
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
            var rows = callbacks.Chunk(2).Select(row => row.Select(x => (object)new { text = x.Text, callback_data = x.Data }).ToArray()).ToList();
            if (buttons is { Length: > 0 })
                rows.AddRange(buttons.Select(x => new object[] { new { text = x.Text, url = x.Url } }));
            keyboard = new { inline_keyboard = rows.ToArray() };
        }
        var target = reply?.Chat == chatId ? reply : null;
        // ReplyKeyboardRemove makes the sent message uneditable in Telegram.
        // Interactive replies must start with an inline keyboard (possibly empty).
        if (target is not null && !contactButton && buttons is not { Length: > 0 } && callbacks is not { Length: > 0 })
            keyboard = new { inline_keyboard = Array.Empty<object>() };
        long? obsolete = null;
        if (target?.Message is { } id && !contactButton)
        {
            try
            {
                await CallAsync("editMessageText", new { chat_id = chatId, message_id = id, text,
                    reply_markup = callbacks is { Length: > 0 } || buttons is { Length: > 0 }
                        ? keyboard : new { inline_keyboard = Array.Empty<object>() },
                    link_preview_options = new { is_disabled = true } }, ct);
                target.Waiting = false;
                return;
            }
            catch (TelegramDeliveryException ex) when (ex.Status == 400)
            {
                // The old message may have been deleted or become uneditable.
                obsolete = id;
                target.Message = null;
            }
        }
        var result = await CallAsync("sendMessage", new { chat_id = chatId, text, reply_markup = keyboard,
            link_preview_options = new { is_disabled = true } }, ct);
        if (target is not null)
        {
            target.Waiting = false;
            if (contactButton) obsolete = target.Message;
            target.Message = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("message_id", out var sent) && sent.TryGetInt64(out var sentId) ? sentId : null;
            if (obsolete is { } waiting)
            {
                // Delete only after the replacement succeeds; cleanup failure must not discard that valid reply.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await CallAsync("deleteMessage", new { chat_id = chatId, message_id = waiting }, cleanup.Token); }
                catch (Exception ex) when (ex is TelegramDeliveryException or OperationCanceledException) { }
            }
        }
    }

    public async Task AnswerCallbackAsync(string id, CancellationToken ct)
    {
        try { await CallAsync("answerCallbackQuery", new { callback_query_id = id }, ct); }
        // Old buttons remain usable after Telegram's short acknowledgement window has expired.
        catch (TelegramDeliveryException ex) when (ex.Status == 400) { }
    }

    public async Task SynchronizeCommandsAsync(CancellationToken ct)
    {
        foreach (var language in new[] { "", "ar", "en" })
        {
            var en = language == "en";
            var commands = new[]
            {
                new { command = "start", description = en ? "Start and link your account" : "بدء الاستخدام وربط الحساب" },
                new { command = "search", description = en ? "Search jobs with filters" : "البحث عن وظائف بالفلاتر" },
                new { command = "jobs", description = en ? "Browse and search jobs" : "تصفح الوظائف والبحث" },
                new { command = "status", description = en ? "Account and subscription status" : "حالة الحساب والاشتراك" },
                new { command = "payment_status", description = en ? "Check a payment" : "التحقق من حالة طلب دفع" },
                new { command = "stop", description = en ? "Stop opportunity alerts" : "إيقاف تنبيهات الفرص" },
                new { command = "help", description = en ? "Help" : "المساعدة" },
            };
            foreach (var scope in new[] { "default", "all_private_chats" })
                await CallAsync("setMyCommands", new { commands, scope = new { type = scope }, language_code = language }, ct);
        }
    }

    private async Task<JsonElement> CallAsync(string method, object payload, CancellationToken ct)
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
        {
            if (method == "editMessageText" && (int)result.StatusCode == 400)
            {
                try
                {
                    using var error = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
                    if (error.RootElement.TryGetProperty("description", out var description)
                        && description.ValueKind == JsonValueKind.String
                        && (description.GetString()?.Contains("message is not modified", StringComparison.OrdinalIgnoreCase) ?? false))
                        return default;
                }
                catch (JsonException) { }
            }
            throw new TelegramDeliveryException((int)result.StatusCode);
        }
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new TelegramDeliveryException(502);
        return json.RootElement.TryGetProperty("result", out var value) ? value.Clone() : default;
    }
}
