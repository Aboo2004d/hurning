using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public sealed class WalletRequestRejectedException : Exception
{
    public WalletRequestRejectedException()
        : base("WalletRequestRejected") { }
}

public sealed class WalletOtpSession(string sessionKey, decimal amount, int expiresIn)
{
    public string SessionKey { get; } = sessionKey;
    public decimal Amount { get; } = amount;
    public int ExpiresIn { get; } = expiresIn;

    public override string ToString() => "Wallet OTP session";
}

public sealed record WalletReceipt(
    long TransactionId,
    string? Reference,
    decimal Amount,
    decimal JawwalFee,
    decimal PlatformFee,
    decimal NetCredited
);

public sealed record WalletBalance(
    decimal Balance,
    decimal Available,
    decimal GrossTotal,
    decimal JawwalFees,
    decimal PlatformFees,
    decimal CreditedTotal,
    decimal WithdrawnTotal,
    decimal PendingWithdrawals,
    long TransactionsCount
);

public static class JawwalPayRules
{
    public static string ExternalReference(Guid orderId) => "hiring-" + orderId.ToString("N");

    public static bool SupportsAmount(decimal amount) =>
        amount is >= 1 and <= 5000 && decimal.Round(amount, 2) == amount;

    public static string Phone(string? value)
    {
        var phone = value?.Trim() ?? "";
        if (
            phone.Length != 10
            || !phone.StartsWith("05", StringComparison.Ordinal)
            || !phone.All(char.IsAsciiDigit)
        )
            throw new InvalidOperationException("WalletMobileInvalid");
        return phone;
    }

    public static bool ValidOtp(string? value) =>
        value is { Length: >= 1 and <= 32 } && value.All(char.IsAsciiDigit);
}

// store.portal.ps is the supplied partner service, not a guessed direct wallet API.
// Confirm is a debit operation and is never used as a status query or retried.
public sealed class JawwalPayGateway(
    HttpClient http,
    IConfiguration configuration,
    HiringDb db,
    PaymentEnvironment? environment = null
) : IPaymentGateway
{
    public string Key => "JawwalPay";
    public string Environment => environment?.Name ?? "Production";
    public bool Sandbox => Environment == "Sandbox";
    private string ApiKey => configuration["Payments:JawwalPay:" + Environment + ":ApiKey"] ?? "";
    private string InitiatePath =>
        Sandbox ? "/api/partner/test/pay/initiate" : "/api/partner/pay/initiate";
    private string ConfirmPath =>
        Sandbox ? "/api/partner/test/pay/confirm" : "/api/partner/pay/confirm";
    public bool Ready =>
        configuration["Payments:JawwalPay:BaseUrl"]?.TrimEnd('/') == "https://store.portal.ps"
        && configuration["Payments:JawwalPay:ApiKeyHeader"] == "X-Partner-Key"
        && ApiKey is { Length: > 0 } key
        && !string.IsNullOrWhiteSpace(key)
        && !key.Contains('*')
        && key.All(character => character >= ' ' && character <= '~')
        && configuration["Payments:JawwalPay:InitiatePath"] == "/api/partner/pay/initiate"
        && configuration["Payments:JawwalPay:ConfirmPath"] == "/api/partner/pay/confirm"
        && configuration["Payments:JawwalPay:BalancePath"] == "/api/partner/balance"
        && configuration["Payments:JawwalPay:TransactionsPath"] == "/api/partner/transactions"
        && configuration["Payments:JawwalPay:TestPingPath"] == "/api/partner/test/ping"
        && configuration["Payments:JawwalPay:TestInitiatePath"] == "/api/partner/test/pay/initiate"
        && configuration["Payments:JawwalPay:TestConfirmPath"] == "/api/partner/test/pay/confirm";

    public bool SupportsAmount(decimal amount) => JawwalPayRules.SupportsAmount(amount);

    public Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct) =>
        throw new InvalidOperationException("WalletRequiresOtpCheckout");

    public async Task<WalletOtpSession> InitiateAsync(
        PaymentOrder order,
        string phone,
        CancellationToken ct
    )
    {
        if (
            order.GatewayEnvironment != Environment
            || order.Currency != "ILS"
            || !SupportsAmount(order.AmountIls)
        )
            throw new PaymentProviderException();
        using var json = await SendAsync(
            HttpMethod.Post,
            InitiatePath,
            new
            {
                phone = JawwalPayRules.Phone(phone),
                amount = order.AmountIls,
                external_ref = JawwalPayRules.ExternalReference(order.Id),
                description = "Employment in Palestine subscription",
            },
            ct,
            allowRejection: true
        );
        return ParseSession(json.RootElement, order);
    }

    public static WalletOtpSession ParseSession(JsonElement root, PaymentOrder order)
    {
        try
        {
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True)
                throw new PaymentProviderException();
            var key = root.GetProperty("session_key").GetString();
            var amount = root.GetProperty("amount").GetDecimal();
            var expires = root.GetProperty("expires_in").GetInt32();
            if (
                key is null
                || key.Length is < 1 or > 1024
                || amount != order.AmountIls
                || expires <= 0
            )
                throw new PaymentProviderException();
            return new(key, amount, expires);
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
            )
        {
            throw new PaymentProviderException();
        }
    }

    public async Task<WalletReceipt> ConfirmAsync(
        PaymentOrder order,
        string sessionKey,
        string otp,
        CancellationToken ct
    )
    {
        if (
            order.GatewayEnvironment != Environment
            || order.Currency != "ILS"
            || !SupportsAmount(order.AmountIls)
        )
            throw new PaymentProviderException();
        if (!JawwalPayRules.ValidOtp(otp))
            throw new InvalidOperationException("WalletOtpInvalid");
        using var json = await SendAsync(
            HttpMethod.Post,
            ConfirmPath,
            new { session_key = sessionKey, otp },
            ct,
            allowRejection: true
        );
        // The direct response belongs to the previously persisted, server-held session.
        // external_ref is nullable in this particular response according to the contract.
        return ParseReceipt(json.RootElement, order, transactionList: false);
    }

    public static WalletReceipt ParseReceipt(
        JsonElement item,
        PaymentOrder order,
        bool transactionList
    )
    {
        try
        {
            if (!transactionList && item.GetProperty("ok").ValueKind != JsonValueKind.True)
                throw new PaymentProviderException();
            var external = item.GetProperty("external_ref");
            if (
                (external.ValueKind != JsonValueKind.Null || transactionList)
                && external.GetString() != JawwalPayRules.ExternalReference(order.Id)
            )
                throw new PaymentProviderException();
            var id = item.GetProperty(transactionList ? "id" : "transaction_id").GetInt64();
            var amount = item.GetProperty("amount").GetDecimal();
            var reference = item.GetProperty(transactionList ? "jawwal_reference" : "reference")
                .GetString();
            if (transactionList && item.GetProperty("status").GetString() != "success")
                throw new PaymentProviderException();
            var fee = item.GetProperty("jawwal_fee").GetDecimal();
            var platformFee = item.GetProperty("platform_fee").GetDecimal();
            var net = item.GetProperty(transactionList ? "net_amount" : "net_credited")
                .GetDecimal();
            if (
                id <= 0
                || amount != order.AmountIls
                || order.Currency != "ILS"
                || reference?.Length > 200
                || fee < 0
                || platformFee < 0
                || net < 0
                || fee > amount
                || platformFee > amount
                || net > amount
                || Math.Abs(fee + platformFee + net - amount) > .01m
            )
                throw new PaymentProviderException();
            return new(id, reference, amount, fee, platformFee, net);
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
                        or OverflowException
            )
        {
            throw new PaymentProviderException();
        }
    }

    public async Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct)
    {
        if (
            order.GatewayEnvironment != Environment
            || order.Currency != "ILS"
            || !SupportsAmount(order.AmountIls)
        )
            throw new PaymentProviderException();
        var session = await db
            .WalletPaymentSessions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrderId == order.Id && x.GatewayEnvironment == Environment,
                ct
            );
        if (session?.TransactionId is { } id && session.ConfirmedAmount == order.AmountIls)
            return new(
                true,
                order.Id,
                "portal:" + id.ToString(CultureInfo.InvariantCulture),
                order.AmountIls,
                "ILS",
                PaymentState.Paid
            );
        // No sandbox transaction-list endpoint was supplied. Never query live data
        // to recover a sandbox session. A persisted sandbox receipt still works.
        if (Sandbox || session?.ConfirmationStartedAt is null)
            return Uncertain(order);
        using var json = await SendAsync(HttpMethod.Get, "/api/partner/transactions", null, ct);
        var receipt = FindReceipt(json.RootElement, order, session.Phone);
        if (receipt is null)
        {
            if (HasConfirmedFailure(json.RootElement, order, session.Phone))
                return new(false, order.Id, null, order.AmountIls, "ILS", PaymentState.Failed);
            return Uncertain(order);
        }
        await SaveReceiptAsync(order.Id, receipt, ct);
        return new(
            true,
            order.Id,
            "portal:" + receipt.TransactionId.ToString(CultureInfo.InvariantCulture),
            receipt.Amount,
            "ILS",
            PaymentState.Paid
        );
    }

    public static WalletReceipt? FindReceipt(JsonElement root, PaymentOrder order, string phone)
    {
        try
        {
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True)
                throw new PaymentProviderException();
            var matching = root.GetProperty("transactions")
                .EnumerateArray()
                .Where(item =>
                    item.GetProperty("external_ref").ValueKind == JsonValueKind.String
                    && item.GetProperty("external_ref").GetString()
                        == JawwalPayRules.ExternalReference(order.Id)
                    && item.GetProperty("status").GetString() == "success"
                )
                .ToArray();
            if (matching.Length == 0)
                return null;
            // Multiple successful charges for one order require manual investigation.
            if (matching.Length != 1 || matching[0].GetProperty("phone").GetString() != phone)
                throw new PaymentProviderException();
            return ParseReceipt(matching[0], order, transactionList: true);
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
            )
        {
            throw new PaymentProviderException();
        }
    }

    public static bool HasConfirmedFailure(JsonElement root, PaymentOrder order, string phone)
    {
        try
        {
            var rows = root.GetProperty("transactions")
                .EnumerateArray()
                .Where(item =>
                    item.GetProperty("external_ref").ValueKind == JsonValueKind.String
                    && item.GetProperty("external_ref").GetString()
                        == JawwalPayRules.ExternalReference(order.Id)
                )
                .ToArray();
            return rows.Length == 1
                && rows[0].GetProperty("status").GetString() == "failed"
                && rows[0].GetProperty("phone").GetString() == phone
                && rows[0].GetProperty("amount").GetDecimal() == order.AmountIls;
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
            )
        {
            throw new PaymentProviderException();
        }
    }

    private static GatewayCheck Uncertain(PaymentOrder order) =>
        new(
            false,
            order.Id,
            null,
            null,
            null,
            order.State == PaymentState.Failed ? PaymentState.Failed : PaymentState.Pending,
            Inconclusive: true
        );

    public async Task SaveReceiptAsync(Guid orderId, WalletReceipt receipt, CancellationToken ct)
    {
        var changed = await db
            .WalletPaymentSessions.Where(x =>
                x.OrderId == orderId
                && x.GatewayEnvironment == Environment
                && (x.TransactionId == null || x.TransactionId == receipt.TransactionId)
            )
            .ExecuteUpdateAsync(
                update =>
                    update
                        .SetProperty(x => x.TransactionId, receipt.TransactionId)
                        .SetProperty(x => x.JawwalReference, receipt.Reference)
                        .SetProperty(x => x.ConfirmedAmount, receipt.Amount)
                        .SetProperty(x => x.JawwalFee, receipt.JawwalFee)
                        .SetProperty(x => x.PlatformFee, receipt.PlatformFee)
                        .SetProperty(x => x.NetCredited, receipt.NetCredited)
                        .SetProperty(x => x.ConfirmedAt, DateTimeOffset.UtcNow),
                ct
            );
        if (changed != 1)
            throw new PaymentProviderException();
    }

    public async Task ProbeAsync(CancellationToken ct)
    {
        if (Sandbox)
        {
            using var json = await SendAsync(HttpMethod.Get, "/api/partner/test/ping", null, ct);
        }
        else
        {
            await BalanceAsync(ct);
        }
    }

    public async Task<WalletBalance> BalanceAsync(CancellationToken ct)
    {
        if (Sandbox)
            throw new PaymentProviderException();
        using var json = await SendAsync(HttpMethod.Get, "/api/partner/balance", null, ct);
        try
        {
            var root = json.RootElement;
            return new(
                root.GetProperty("balance").GetDecimal(),
                root.GetProperty("available").GetDecimal(),
                root.GetProperty("gross_total").GetDecimal(),
                root.GetProperty("jawwal_fees_total").GetDecimal(),
                root.GetProperty("platform_fees_total").GetDecimal(),
                root.GetProperty("credited_total").GetDecimal(),
                root.GetProperty("withdrawn_total").GetDecimal(),
                root.GetProperty("pending_withdrawals").GetDecimal(),
                root.GetProperty("transactions_count").GetInt64()
            );
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
            )
        {
            throw new PaymentProviderException();
        }
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct,
        bool allowRejection = false
    )
    {
        if (!Ready)
            throw new PaymentProviderException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var request = new HttpRequestMessage(method, "https://store.portal.ps" + path);
            request.Headers.Add("X-Partner-Key", ApiKey);
            if (body != null)
                request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token
            );
            if (
                !response.IsSuccessStatusCode
                && !(allowRejection && response.StatusCode == HttpStatusCode.UnprocessableEntity)
            )
                throw new PaymentProviderException();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int size;
            while ((size = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (bounded.Length + size > 262144)
                    throw new PaymentProviderException();
                bounded.Write(buffer, 0, size);
            }
            var json = JsonDocument.Parse(bounded.ToArray());
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("ok", out var ok)
            )
            {
                json.Dispose();
                throw new PaymentProviderException();
            }
            if (
                allowRejection
                && response.StatusCode == HttpStatusCode.UnprocessableEntity
                && ok.ValueKind == JsonValueKind.False
            )
            {
                json.Dispose();
                throw new WalletRequestRejectedException();
            }
            if (!response.IsSuccessStatusCode || ok.ValueKind != JsonValueKind.True)
            {
                json.Dispose();
                throw new PaymentProviderException();
            }
            return json;
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or InvalidOperationException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            throw new PaymentProviderException();
        }
    }
}

public static class JawwalPayRegistration
{
    public static IServiceCollection AddJawwalPay(this IServiceCollection services)
    {
        services
            .AddHttpClient<JawwalPayGateway>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            )
            .RemoveAllLoggers();
        services.AddScoped<IPaymentGateway>(provider =>
            provider.GetRequiredService<JawwalPayGateway>()
        );
        return services;
    }
}
