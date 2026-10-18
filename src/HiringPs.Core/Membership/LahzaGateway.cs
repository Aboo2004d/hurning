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
    public PaymentProviderException()
        : base("GatewayUnavailable") { }
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
    public bool Ready =>
        !string.IsNullOrWhiteSpace(Secret)
        && !Secret.Contains('*')
        && Secret.StartsWith(Sandbox ? "sk_test_" : "sk_live_", StringComparison.Ordinal)
        && Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin)
        && origin.Scheme == "https"
        && origin.IsDefaultPort
        && origin.AbsolutePath == "/"
        && origin.UserInfo.Length == 0
        && origin.Query.Length == 0
        && origin.Fragment.Length == 0
        && !origin.Host.Contains("YOUR-", StringComparison.OrdinalIgnoreCase);

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
            throw new PaymentProviderException();
        var reference = Reference(order.Id);
        if (order.ProviderReference != reference)
            throw new PaymentProviderException();
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
            throw new PaymentProviderException();
        }
    }

    public static GatewayCheck ParseCheck(JsonElement response, PaymentOrder order, bool testMode)
    {
        var data = response.GetProperty("data");
        var reference = data.GetProperty("reference").GetString();
        var amount = data.GetProperty("amount").GetDecimal() / 100m;
        var currency = data.GetProperty("currency").GetString();
        if (
            reference != Reference(order.Id)
            || amount != order.AmountIls
            || currency != "ILS"
            || !MatchesOptionalDomain(data, testMode)
        )
            throw new PaymentProviderException();
        var status = data.GetProperty("status").GetString();
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
            throw new PaymentProviderException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var operation = timeout.Token;
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
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 262144)
                throw new PaymentProviderException();
            await using var stream = await response.Content.ReadAsStreamAsync(operation);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, operation)) > 0)
            {
                if (bounded.Length + count > 262144)
                    throw new PaymentProviderException();
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
                        or KeyNotFoundException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            // Provider bodies and authorization headers must never reach logs or the browser.
            throw new PaymentProviderException();
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
