using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يفحص هوية البوت وWebhook ويسجله بطلب صريح من الإدارة؛ لا يرسل رسائل أو يحذف تحديثات.</summary>
public sealed class TelegramBotSetupService(HttpClient http, IConfiguration configuration)
{
    public const string WebhookPath = "/integrations/telegram";
    private string Token { get; } = configuration["Telegram:BotToken"] ?? "";
    private string Secret { get; } = configuration["Telegram:WebhookSecret"] ?? "";

    /// <summary>يفحص الإعدادات الفعلية للمضيف ثم getMe وgetWebhookInfo دون أي تغيير خارجي.</summary>
    public async Task<TelegramBotStatusDto> CheckAsync(CancellationToken ct)
    {
        var initial = InitialStatus();
        if (!initial.Configured) return initial;
        try
        {
            var identity = await CallAsync("getMe", null, ct);
            var username = Text(identity, "username");
            if (!identity.TryGetProperty("is_bot", out var bot) || bot.ValueKind != JsonValueKind.True
                || !string.Equals(username, initial.BotUsername, StringComparison.OrdinalIgnoreCase))
                return initial with { State = TelegramBotSetupState.IdentityMismatch };
            var info = await CallAsync("getWebhookInfo", null, ct);
            var url = Text(info, "url");
            var state = url.Length == 0 ? TelegramBotSetupState.NotRegistered
                : url != initial.ExpectedWebhook ? TelegramBotSetupState.DifferentWebhook
                : TelegramBotSetupState.Registered;
            if (state == TelegramBotSetupState.Registered && info.TryGetProperty("allowed_updates", out var updates)
                && updates.ValueKind == JsonValueKind.Array && updates.GetArrayLength() > 0
                && !updates.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == "message"))
                state = TelegramBotSetupState.UpdatesDisabled;
            DateTimeOffset? lastError = null;
            if (info.TryGetProperty("last_error_date", out var date) && date.ValueKind == JsonValueKind.Number && date.TryGetInt64(out var seconds))
            {
                try { lastError = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                catch (ArgumentOutOfRangeException) { }
            }
            return initial with
            {
                State = state, CurrentWebhook = Clean(url),
                PendingUpdates = info.TryGetProperty("pending_update_count", out var pending) && pending.ValueKind == JsonValueKind.Number && pending.TryGetInt64(out var count) ? count : null,
                LastErrorAt = lastError, LastError = Clean(Text(info, "last_error_message")),
            };
        }
        catch (TelegramBotSetupException ex)
        {
            return initial with { State = TelegramBotSetupState.Unavailable, FailureCode = ex.Code, FailureDescription = ex.Message };
        }
    }

    /// <summary>يسجل عنوان الموقع وسر التحقق دون إسقاط الرسائل المعلقة، ثم يتحقق من العنوان المسجل.</summary>
    public async Task<TelegramBotSetupResultDto> RegisterAsync(CancellationToken ct)
    {
        var before = await CheckAsync(ct);
        if (!before.Configured || before.State is TelegramBotSetupState.IdentityMismatch or TelegramBotSetupState.Unavailable)
            return new(false, before);
        try
        {
            await CallAsync("setWebhook", new
            {
                url = before.ExpectedWebhook, secret_token = Secret,
                allowed_updates = new[] { "message" }, drop_pending_updates = false,
            }, ct);
        }
        catch (TelegramBotSetupException ex)
        {
            return new(false, before with { State = TelegramBotSetupState.Unavailable, FailureCode = ex.Code, FailureDescription = ex.Message });
        }
        return new(true, await CheckAsync(ct));
    }

    /// <summary>يتحقق من إعدادات السيرفر قبل إجراء أي اتصال خارجي.</summary>
    private TelegramBotStatusDto InitialStatus()
    {
        var status = new TelegramBotStatusDto { BotUsername = Clean(configuration["Telegram:BotUsername"] ?? "") };
        if (!configuration.GetValue<bool>("Telegram:Enabled"))
            return status with { State = TelegramBotSetupState.Disabled };
        string origin;
        try { origin = PublicUrls.Origin(configuration); }
        catch (InvalidOperationException) { return status with { State = TelegramBotSetupState.InvalidConfiguration, FailureCode = "Origin" }; }
        status = status with { ExpectedWebhook = origin + WebhookPath };
        var invalid = !Regex.IsMatch(Token, @"\A[0-9]+:[A-Za-z0-9_-]{20,}\z") ? "Token"
            : !Regex.IsMatch(status.BotUsername, @"\A[A-Za-z0-9_]{5,32}\z") ? "Username"
            : !Regex.IsMatch(Secret, @"\A[A-Za-z0-9_-]{24,256}\z") ? "Secret" : "";
        return status with { Configured = invalid.Length == 0, State = TelegramBotSetupState.InvalidConfiguration, FailureCode = invalid };
    }

    /// <summary>ينفذ طلب Bot API محدودًا ويحجب عنوانه السري من الاستثناءات المعادة.</summary>
    private async Task<JsonElement> CallAsync(string method, object? payload, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(payload is null ? HttpMethod.Get : HttpMethod.Post,
                "https://api.telegram.org/bot" + Token + "/" + method);
            if (payload is not null) request.Content = JsonContent.Create(payload);
            using var response = await http.SendAsync(request, ct);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (!response.IsSuccessStatusCode || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
                throw new TelegramBotSetupException("Provider", $"HTTP {(int)response.StatusCode}; " + Clean(Text(root, "description")));
            if (!root.TryGetProperty("result", out var result) || (method == "setWebhook" && result.ValueKind != JsonValueKind.True))
                throw new TelegramBotSetupException("InvalidResponse", "");
            if (method != "setWebhook" && result.ValueKind != JsonValueKind.Object)
                throw new TelegramBotSetupException("InvalidResponse", "");
            if (method == "getWebhookInfo" && (!result.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String))
                throw new TelegramBotSetupException("InvalidResponse", "");
            return result.Clone();
        }
        catch (HttpRequestException ex) { throw new TelegramBotSetupException("Network", ex.HttpRequestError.ToString()); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TelegramBotSetupException("Timeout", ""); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new TelegramBotSetupException("InvalidResponse", ""); }
    }

    private static string Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString()! : "";

    /// <summary>يحجب الرمز وسر Webhook إن أعادهما المزود ضمن نص التشخيص.</summary>
    private string Clean(string value)
    {
        foreach (var secret in new[] { Token, Secret }.Where(x => x.Length > 0))
            value = value.Replace(secret, "[redacted]", StringComparison.Ordinal)
                .Replace(Uri.EscapeDataString(secret), "[redacted]", StringComparison.Ordinal);
        return value.Truncate(1000);
    }
}
