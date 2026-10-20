using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

// Hosted checkout: card data never enters this application.
/// <summary>محول لحظة للدفع المستضاف والتحقق من الإيصالات والتوقيع وفق عقد المزود.</summary>
public sealed class LahzaGateway : IPaymentGateway
{
    private readonly LahzaApiClient api;
    private readonly IConfiguration configuration;
    private readonly PaymentEnvironment? environment;
    private readonly IPaymentGatewayData data;

    /// <summary>ينشئ محول المزود مع حد بيانات مستقل عن EF؛ هذا هو مسار حقن الاعتماديات.</summary>
    [ActivatorUtilitiesConstructor]
    public LahzaGateway(HttpClient http, IConfiguration configuration, IPaymentGatewayData data, PaymentEnvironment? environment = null)
    {
        api = new LahzaApiClient(http);
        this.configuration = configuration;
        this.data = data;
        this.environment = environment;
    }

    /// <summary>يحافظ على توافق المستدعين الحاليين الذين ينشئون المحول بسياق قاعدة مباشر.</summary>
    public LahzaGateway(HttpClient http, IConfiguration configuration, HiringDb db, PaymentEnvironment? environment = null)
        : this(http, configuration, new PaymentGatewayData(db), environment) { }

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

    /// <summary>ينشئ مرجع طلب لحظة الثابت الذي يستخدم للمطابقة مع إيصال المزود.</summary>
    public static string Reference(Guid id) => "hiring-" + id.ToString("N");

    /// <summary>يتحقق من أن رابط الدفع يتبع نطاق لحظة والبروتوكول والمنفذ المسموح بها.</summary>
    public static bool IsCheckoutUrl(Uri url) =>
        url.Scheme == "https"
        && url.Host.Equals("checkout.lahza.io", StringComparison.OrdinalIgnoreCase)
        && url.IsDefaultPort
        && string.IsNullOrEmpty(url.UserInfo)
        && url.AbsoluteUri.Length <= 2048;

    /// <summary>يتحقق من توقيع إشعار لحظة بمقارنة ثابتة الزمن دون الوثوق ببيانات المتصفح.</summary>
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

    /// <summary>ينشئ رابط الدفع المستضاف أو يرفض المسار غير المدعوم وفق عقد البوابة.</summary>
    public async Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct)
    {
        if (order.GatewayEnvironment != Environment)
            throw new PaymentProviderException();
        var reference = Reference(order.Id);
        if (order.ProviderReference != reference)
            throw new PaymentProviderException();
        var buyer = await this.data.BuyerAsync(order.UserId, ct);
        var origin = PublicOrigin!.TrimEnd('/');
        var language = buyer.Language;
        using var data = await SendAsync(
            HttpMethod.Post,
            "transaction/initialize",
            new
            {
                amount = checked((long)(order.AmountIls * 100)).ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                currency = "ILS",
                email = buyer.Email,
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

    /// <summary>يتحقق من عملية لحظة عبر API ويطابق المرجع والمبلغ والعملة والبيئة قبل تأكيدها.</summary>
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

    /// <summary>يحلل نتيجة تحقق لحظة ويطابق المرجع والمبلغ والعملة والبيئة بطلب الموقع.</summary>
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

    /// <summary>يقرأ حقلًا نصيًا مطلوبًا من رد التحقق ويرفض صيغة غير متوقعة.</summary>
    private static string ReadVerificationString(JsonElement data, string field, string code)
    {
        if (!data.TryGetProperty(field, out var value))
            throw new PaymentProviderException(code + "Missing", 200);
        if (value.ValueKind != JsonValueKind.String)
            throw new PaymentProviderException(code + "Type" + value.ValueKind, 200);
        return value.GetString()!;
    }

    /// <summary>يقرأ مبلغ المزود بوحداته الصغرى مع منع القيم غير المطابقة للعقد.</summary>
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

    /// <summary>يطابق نطاق البيئة عند توفره في رد المزود دون تخمين قيمة غائبة.</summary>
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

    /// <summary>يفحص الاتصال أو الهوية والصلاحيات دون تنفيذ دفعة أو إنشاء منشور.</summary>
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

    /// <summary>يرسل الطلب إلى المزود ويتحقق من الاستجابة والمهلة وفق عقد هذه الخدمة.</summary>
    private Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct) =>
        api.SendAsync(method, path, body, Secret, Ready, ConfigurationStatus, ct);

}
