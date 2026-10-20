using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

// store.portal.ps is the supplied partner service, not a guessed direct wallet API.
// Confirm is a debit operation and is never used as a status query or retried.
/// <summary>محول جوال باي لجلسة OTP والخصم والتحقق من الإيصالات والرصيد؛ التأكيد ليس استعلام حالة.</summary>
public sealed class JawwalPayGateway : IPaymentGateway
{
    private readonly JawwalPayApiClient api;
    private readonly IConfiguration configuration;
    private readonly PaymentEnvironment? environment;
    private readonly IPaymentGatewayData data;

    /// <summary>ينشئ محول المزود مع حد بيانات مستقل عن EF؛ هذا هو مسار حقن الاعتماديات.</summary>
    [ActivatorUtilitiesConstructor]
    public JawwalPayGateway(HttpClient http, IConfiguration configuration, IPaymentGatewayData data, PaymentEnvironment? environment = null)
    {
        api = new JawwalPayApiClient(http);
        this.configuration = configuration;
        this.data = data;
        this.environment = environment;
    }

    /// <summary>يحافظ على توافق المستدعين الحاليين الذين ينشئون المحول بسياق قاعدة مباشر.</summary>
    public JawwalPayGateway(HttpClient http, IConfiguration configuration, HiringDb db, PaymentEnvironment? environment = null)
        : this(http, configuration, new PaymentGatewayData(db), environment) { }

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

    /// <summary>يتحقق من أن المبلغ يقع ضمن حدود البوابة ودقة الكسور المدعومة.</summary>
    public bool SupportsAmount(decimal amount) => JawwalPayRules.SupportsAmount(amount);

    /// <summary>ينشئ رابط الدفع المستضاف أو يرفض المسار غير المدعوم وفق عقد البوابة.</summary>
    public Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct) =>
        throw new InvalidOperationException("WalletRequiresOtpCheckout");

    /// <summary>يبدأ جلسة المحفظة بعد التحقق من ملكية الطلب والرقم والمبلغ والبيئة.</summary>
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

    /// <summary>يحلل جلسة OTP ويرفض مفتاحًا أو مبلغًا أو مدة لا تطابق العقد.</summary>
    public static WalletOtpSession ParseSession(JsonElement root, PaymentOrder order)
    {
        try
        {
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True)
                throw new PaymentProviderException();
            var key = ReadText(root, "session_key");
            var amount = ReadMoney(root, "amount");
            var expires = ReadInteger(root, "expires_in");
            if (
                key is null
                || key.Length is < 1 or > 1024
                || amount != order.AmountIls
                || expires <= 0
                || expires > int.MaxValue
            )
                throw new PaymentProviderException();
            return new(key, amount, (int)expires);
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

    /// <summary>يؤكد العملية وفق قواعد الخدمة مع منع إعادة تنفيذ التأكيد غير الآمن.</summary>
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
        return ParseReceipt(json.RootElement, order, transactionList: false, sandbox: Sandbox);
    }

    /// <summary>يحلل إيصال المحفظة ويطابق المبلغ والمرجع والرسوم قبل اعتماده.</summary>
    public static WalletReceipt ParseReceipt(
        JsonElement item,
        PaymentOrder order,
        bool transactionList,
        bool sandbox = false
    )
    {
        try
        {
            if (!transactionList && item.GetProperty("ok").ValueKind != JsonValueKind.True)
                throw new PaymentProviderException();
            var external = ReadText(item, "external_ref", optional: !transactionList);
            if (
                (external is not null || transactionList)
                && external != JawwalPayRules.ExternalReference(order.Id)
            )
                throw new PaymentProviderException("WalletExternalReferenceMismatch", 200);
            var id = ReadInteger(item, transactionList ? "id" : "transaction_id");
            var amount = ReadMoney(item, "amount");
            var reference = ReadText(
                item,
                transactionList ? "jawwal_reference" : "reference",
                optional: true
            );
            if (transactionList && item.GetProperty("status").GetString() != "success")
                throw new PaymentProviderException();
            var fee = ReadMoney(item, "jawwal_fee");
            var platformFee = ReadMoney(item, "platform_fee");
            var net = ReadMoney(item, transactionList ? "net_amount" : "net_credited");
            var facts = new WalletReceiptValidationFacts(
                id,
                order.AmountIls,
                amount,
                fee,
                platformFee,
                net
            );
            PaymentProviderException Reject(string code) => new(code, 200) { ReceiptFacts = facts };
            if (id <= 0)
                throw Reject("WalletTransactionIdInvalid");
            if (amount != order.AmountIls)
                throw Reject("WalletAmountMismatch");
            if (order.Currency != "ILS")
                throw Reject("WalletCurrencyMismatch");
            if (reference?.Length > 200)
                throw Reject("WalletReferenceTooLong");
            if (fee < 0 || fee > amount)
                throw Reject("WalletJawwalFeeInvalid");
            if (platformFee < 0 || platformFee > amount)
                throw Reject("WalletPlatformFeeInvalid");
            if (net < 0 || net > amount)
                throw Reject("WalletNetAmountInvalid");
            // The supplied Sandbox response reports success without crediting a balance:
            // gross=5, wallet fee=.30, net=0. Preserve those values as a test receipt.
            // The live adapter and transaction-list recovery never use this exception.
            var sandboxSimulation =
                sandbox
                && !transactionList
                && order.GatewayEnvironment == "Sandbox"
                && net == 0
                && fee + platformFee <= amount;
            if (Math.Abs(fee + platformFee + net - amount) > .01m && !sandboxSimulation)
                throw Reject("WalletFeeSumMismatch");
            return new(id, reference, amount, fee, platformFee, net)
            {
                IsSandboxSimulation = sandboxSimulation,
            };
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
            throw new PaymentProviderException("WalletMalformedReceipt", 200);
        }
    }

    /// <summary>يقرأ حقلًا إلزاميًا من JSON ويرفض غيابه أو بنيته غير المتوقعة.</summary>
    private static JsonElement RequiredField(JsonElement root, string field)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(field, out var value))
            throw new PaymentProviderException("WalletField." + field + ".Missing", 200);
        return value;
    }

    /// <summary>يقرأ مبلغًا ماليًا من رد المزود مع التحقق من النوع والدقة.</summary>
    private static decimal ReadMoney(JsonElement root, string field)
    {
        var value = RequiredField(root, field);
        decimal amount;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out amount))
            return amount;
        if (
            value.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                value.GetString(),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount
            )
        )
            return amount;
        throw new PaymentProviderException(
            "WalletField." + field + ".Invalid" + value.ValueKind,
            200
        );
    }

    /// <summary>يقرأ معرّفًا أو عدادًا صحيحًا من رد المزود دون تحويلات متسامحة.</summary>
    private static long ReadInteger(JsonElement root, string field)
    {
        var value = RequiredField(root, field);
        long number;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out number))
            return number;
        if (
            value.ValueKind == JsonValueKind.String
            && long.TryParse(
                value.GetString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out number
            )
        )
            return number;
        throw new PaymentProviderException(
            "WalletField." + field + ".Invalid" + value.ValueKind,
            200
        );
    }

    /// <summary>يقرأ حقلًا نصيًا وفق شرط كونه إلزاميًا أو اختياريًا.</summary>
    private static string? ReadText(JsonElement root, string field, bool optional = false)
    {
        if (
            optional
            && (
                !root.TryGetProperty(field, out var optionalValue)
                || optionalValue.ValueKind == JsonValueKind.Null
            )
        )
            return null;
        var value = RequiredField(root, field);
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        throw new PaymentProviderException(
            "WalletField." + field + ".Invalid" + value.ValueKind,
            200
        );
    }

    /// <summary>يبحث عن إيصال المحفظة القائم دون إعادة تنفيذ طلب الخصم أو تأكيد OTP.</summary>
    public async Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct)
    {
        if (
            order.GatewayEnvironment != Environment
            || order.Currency != "ILS"
            || !SupportsAmount(order.AmountIls)
        )
            throw new PaymentProviderException();
        var session = await data.WalletAsync(order.Id, Environment, ct);
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
        if (Sandbox)
            return Uncertain(
                order,
                session is null ? "WalletSessionMissing"
                    : session.ConfirmationStartedAt is null ? "WalletAwaitingOtpConfirmation"
                    : session.ConfirmationRejected ? "WalletConfirmationRejected"
                    : "WalletSandboxReceiptMissing"
            );
        if (session?.ConfirmationStartedAt is null)
            return Uncertain(order, "WalletAwaitingOtpConfirmation");
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

    /// <summary>يبحث عن إيصال مطابق للطلب والمحفظة بدل اعتماد أول عنصر في قائمة المعاملات.</summary>
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

    /// <summary>يتحقق من وجود رفض صريح يطابق الطلب والمحفظة بدل تفسير غياب الإيصال كفشل.</summary>
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

    /// <summary>ينشئ نتيجة تحقق غير محسومة حتى لا تعتمد دفعة بلا دليل.</summary>
    private static GatewayCheck Uncertain(PaymentOrder order, string? reason = null) =>
        new(
            false,
            order.Id,
            null,
            null,
            null,
            order.State == PaymentState.Failed ? PaymentState.Failed : PaymentState.Pending,
            Inconclusive: true,
            DiagnosticCode: reason
        );

    /// <summary>يحفظ أرقام إيصال المحفظة المؤكد وحقوله المالية المسموح بها.</summary>
    public Task SaveReceiptAsync(Guid orderId, WalletReceipt receipt, CancellationToken ct) =>
        data.SaveWalletReceiptAsync(orderId, Environment, receipt, ct);

    /// <summary>يفحص الاتصال أو الهوية والصلاحيات دون تنفيذ دفعة أو إنشاء منشور.</summary>
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

    /// <summary>يقرأ ملخص رصيد المحفظة ورسومها دون تنفيذ خصم أو سحب.</summary>
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

    /// <summary>يرسل الطلب إلى المزود ويتحقق من الاستجابة والمهلة وفق عقد هذه الخدمة.</summary>
    private Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct, bool allowRejection = false) =>
        api.SendAsync(method, path, body, ApiKey, Ready, ct, allowRejection);

}
