using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public sealed class PaymentProviderException : Exception
{
    public PaymentProviderException(
        string reasonCode = "ProviderUnavailable",
        int? httpStatus = null
    )
        : base("GatewayUnavailable")
    {
        ReasonCode = reasonCode;
        HttpStatus = httpStatus;
    }

    public string ReasonCode { get; }
    public int? HttpStatus { get; }
    public WalletReceiptValidationFacts? ReceiptFacts { get; init; }
}

// Hosted checkout: card data never enters this application.
public sealed class LahzaGateway(
    HttpClient http,
    IConfiguration configuration,
    HiringDb db,
    PaymentEnvironment? environment = null
) : IPaymentGateway
{
    public string Key => "Lahza";
    public string Environment => environment?.Name ?? "Production";
    private bool Sandbox => Environment == "Sandbox";
    private string PublicOrigin =>
        configuration["Payments:Lahza:" + Environment + ":PublicOrigin"] ?? "";
    private string Secret => configuration["Payments:Lahza:" + Environment + ":SecretKey"] ?? "";
    public bool Ready => ConfigurationStatus == "Ready";
    public string ConfigurationStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Secret) || Secret.Contains('*'))
                return "MissingSecret";
            if (!Secret.StartsWith(Sandbox ? "sk_test_" : "sk_live_", StringComparison.Ordinal))
                return "WrongSecretEnvironment";
            if (
                !Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin)
                || origin.Scheme != "https"
                || !origin.IsDefaultPort
                || origin.AbsolutePath != "/"
                || origin.UserInfo.Length != 0
                || origin.Query.Length != 0
                || origin.Fragment.Length != 0
                || origin.Host.Contains("YOUR-", StringComparison.OrdinalIgnoreCase)
            )
                return "InvalidPublicOrigin";
            return "Ready";
        }
    }

    public static string Reference(Guid id) => "hiring-" + id.ToString("N");

    public static bool IsCheckoutUrl(Uri url) =>
        url.Scheme == "https"
        && url.Host.Equals("checkout.lahza.io", StringComparison.OrdinalIgnoreCase)
        && url.IsDefaultPort
        && string.IsNullOrEmpty(url.UserInfo)
        && url.AbsoluteUri.Length <= 2048;

    public static bool VerifySignature(byte[] body, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Contains('*') || signature.Length != 64)
            return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body),
                Convert.FromHexString(signature)
            );
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct)
    {
        if (order.GatewayEnvironment != Environment)
            throw new PaymentProviderException();
        var reference = Reference(order.Id);
        if (order.ProviderReference != reference)
            throw new PaymentProviderException();
        var email = await db
            .Users.Where(x => x.Id == order.UserId)
            .Select(x => x.Email)
            .SingleAsync(ct);
        var origin = PublicOrigin!.TrimEnd('/');
        var language = await db
            .MemberProfiles.Where(x => x.UserId == order.UserId)
            .Select(x => x.Language)
            .SingleAsync(ct);
        using var data = await SendAsync(
            HttpMethod.Post,
            "transaction/initialize",
            new
            {
                amount = checked((long)(order.AmountIls * 100)).ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                currency = "ILS",
                email,
                reference,
                callback_url = origin
                    + "/integrations/lahza/return?lang="
                    + (language == "en" ? "en" : "ar"),
                metadata = JsonSerializer.Serialize(new { orderId = order.Id }),
            },
            ct
        );
        try
        {
            var payload = data.RootElement.GetProperty("data");
            if (
                payload.GetProperty("reference").GetString() != reference
                || !Uri.TryCreate(
                    payload.GetProperty("authorization_url").GetString(),
                    UriKind.Absolute,
                    out var url
                )
                || !IsCheckoutUrl(url)
            )
                throw new PaymentProviderException();
            return new(reference, url);
        }
        catch (Exception ex)
            when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new PaymentProviderException();
        }
    }

    public async Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct)
    {
        if (order.GatewayEnvironment != Environment)
            throw new PaymentProviderException("OrderEnvironmentMismatch");
        var reference = Reference(order.Id);
        if (order.ProviderReference != reference)
            throw new PaymentProviderException("StoredReferenceMismatch");
        using var data = await SendAsync(
            HttpMethod.Get,
            "transaction/verify/" + reference,
            null,
            ct
        );
        try
        {
            return ParseCheck(data.RootElement, order, Sandbox);
        }
        catch (Exception ex)
            when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new PaymentProviderException("MalformedVerificationData", 200);
        }
    }

    public static GatewayCheck ParseCheck(JsonElement response, PaymentOrder order, bool testMode)
    {
        if (
            response.ValueKind != JsonValueKind.Object
            || !response.TryGetProperty("data", out var data)
        )
            throw new PaymentProviderException("DataMissing", 200);
        if (data.ValueKind != JsonValueKind.Object)
            throw new PaymentProviderException("DataType" + data.ValueKind, 200);
        var reference = ReadVerificationString(data, "reference", "Reference");
        var amount = ReadAgora(data) / 100m;
        var currency = ReadVerificationString(data, "currency", "Currency");
        if (reference != Reference(order.Id))
            throw new PaymentProviderException("ReferenceMismatch", 200);
        if (amount != order.AmountIls)
            throw new PaymentProviderException("AmountMismatch", 200);
        if (currency != "ILS")
            throw new PaymentProviderException("CurrencyMismatch", 200);
        if (!MatchesOptionalDomain(data, testMode))
            throw new PaymentProviderException("ResponseEnvironmentMismatch", 200);
        var status = ReadVerificationString(data, "status", "Status");
        return new(
            status == "success",
            order.Id,
            reference,
            amount,
            currency,
            status == "success" ? PaymentState.Paid
                : status == "failed" ? PaymentState.Failed
                : PaymentState.Pending
        );
    }

    private static string ReadVerificationString(JsonElement data, string field, string code)
    {
        if (!data.TryGetProperty(field, out var value))
            throw new PaymentProviderException(code + "Missing", 200);
        if (value.ValueKind != JsonValueKind.String)
            throw new PaymentProviderException(code + "Type" + value.ValueKind, 200);
        return value.GetString()!;
    }

    private static decimal ReadAgora(JsonElement data)
    {
        if (!data.TryGetProperty("amount", out var value))
            throw new PaymentProviderException("AmountMissing", 200);
        decimal agora;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out agora))
                throw new PaymentProviderException("AmountInvalidNumber", 200);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            if (
                !decimal.TryParse(
                    value.GetString(),
                    System.Globalization.NumberStyles.AllowLeadingSign
                        | System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out agora
                )
            )
                throw new PaymentProviderException("AmountInvalidString", 200);
        }
        else
            throw new PaymentProviderException("AmountType" + value.ValueKind, 200);
        if (agora < 0 || agora != decimal.Truncate(agora))
            throw new PaymentProviderException("AmountInvalidAgora", 200);
        return agora;
    }

    private static bool MatchesOptionalDomain(JsonElement data, bool testMode)
    {
        // Lahza can omit domain from verify responses. The request still uses the
        // selected environment's authenticated secret; a supplied domain must agree.
        if (
            !data.TryGetProperty("domain", out var domain)
            || domain.ValueKind == JsonValueKind.Null
        )
            return true;
        if (domain.ValueKind != JsonValueKind.String)
            return false;
        var value = domain.GetString();
        return string.IsNullOrWhiteSpace(value) || value == (testMode ? "test" : "live");
    }

    public async Task ProbeAsync(CancellationToken ct)
    {
        using var json = await SendAsync(HttpMethod.Get, "payment_session_timeout", null, ct);
        if (
            !json.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("payment_session_timeout", out var timeout)
            || !timeout.TryGetInt32(out _)
        )
            throw new PaymentProviderException();
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct
    )
    {
        if (!Ready)
            throw new PaymentProviderException(ConfigurationStatus);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var operation = timeout.Token;
        int? httpStatus = null;
        try
        {
            using var request = new HttpRequestMessage(method, "https://api.lahza.io/" + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Secret);
            if (body != null)
                request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                operation
            );
            httpStatus = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                throw new PaymentProviderException("HttpFailure", httpStatus);
            if (response.Content.Headers.ContentLength > 262144)
                throw new PaymentProviderException("ResponseTooLarge", httpStatus);
            await using var stream = await response.Content.ReadAsStreamAsync(operation);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, operation)) > 0)
            {
                if (bounded.Length + count > 262144)
                    throw new PaymentProviderException("ResponseTooLarge", httpStatus);
                bounded.Write(buffer, 0, count);
            }
            var json = JsonDocument.Parse(bounded.ToArray());
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("status", out var status)
                || status.ValueKind != JsonValueKind.True
            )
            {
                json.Dispose();
                throw new PaymentProviderException("ApiRejected", httpStatus);
            }
            return json;
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            // Provider bodies and authorization headers must never reach logs or the browser.
            throw new PaymentProviderException(
                ex switch
                {
                    OperationCanceledException => "Timeout",
                    HttpRequestException => "NetworkFailure",
                    IOException => "ResponseReadFailure",
                    JsonException => "MalformedJson",
                    _ => "UnexpectedPayload",
                },
                httpStatus
            );
        }
    }
}

public static class LahzaRegistration
{
    public static IServiceCollection AddLahza(this IServiceCollection services)
    {
        services
            .AddHttpClient<LahzaGateway>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            )
            .RemoveAllLoggers();
        services.AddScoped<IPaymentGateway>(provider =>
            provider.GetRequiredService<LahzaGateway>()
        );
        return services;
    }
}
