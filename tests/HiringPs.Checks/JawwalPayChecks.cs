using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class JawwalPayChecks
{
    public static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Payments:JawwalPay:Enabled"] = "true",
                    ["Payments:JawwalPay:Production:ApiKey"] = "fixture_partner_key_no_live_access",
                    ["Payments:JawwalPay:ApiKeyHeader"] = "X-Partner-Key",
                    ["Payments:JawwalPay:BaseUrl"] = "https://store.portal.ps",
                    ["Payments:JawwalPay:InitiatePath"] = "/api/partner/pay/initiate",
                    ["Payments:JawwalPay:ConfirmPath"] = "/api/partner/pay/confirm",
                    ["Payments:JawwalPay:BalancePath"] = "/api/partner/balance",
                    ["Payments:JawwalPay:TransactionsPath"] = "/api/partner/transactions",
                    ["Payments:JawwalPay:Sandbox:ApiKey"] = "fixture_sandbox_key_no_live_access",
                    ["Payments:JawwalPay:TestPingPath"] = "/api/partner/test/ping",
                    ["Payments:JawwalPay:TestInitiatePath"] = "/api/partner/test/pay/initiate",
                    ["Payments:JawwalPay:TestConfirmPath"] = "/api/partner/test/pay/confirm",
                }
            )
            .Build();

    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception("FAIL: " + name);
            count++;
            Console.WriteLine("PASS: " + name);
        }
        var configuration = Configuration();
        await using var db = new HiringDb(
            new DbContextOptionsBuilder<HiringDb>()
                .UseSqlServer(
                    "Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"
                )
                .Options
        );
        var handler = new Handler();
        var gateway = new JawwalPayGateway(new HttpClient(handler), configuration, db);
        var order = new PaymentOrder
        {
            AmountIls = 25,
            Currency = "ILS",
            Gateway = "JawwalPay",
        };
        var external = JawwalPayRules.ExternalReference(order.Id);
        foreach (var amount in new decimal[] { 1, 25, 4999.99m, 5000 })
            Check(gateway.SupportsAmount(amount), "supported ILS amount " + amount);
        foreach (var amount in new decimal[] { 0, .99m, 5000.01m, 1.001m })
            Check(!gateway.SupportsAmount(amount), "unsupported amount " + amount);
        Check(JawwalPayRules.Phone("0599123456") == "0599123456", "documented wallet phone format");
        foreach (
            var phone in new[]
            {
                "+970599123456",
                "599123456",
                "05991234567",
                "٠٥٩٩١٢٣٤٥٦",
                "0599 123456",
            }
        )
        {
            try
            {
                JawwalPayRules.Phone(phone);
                Check(false, "invalid phone rejected");
            }
            catch (InvalidOperationException)
            {
                Check(true, "reject unsupported phone " + phone);
            }
        }
        Check(JawwalPayRules.ValidOtp("123456"), "OTP remains a string");
        Check(
            !JawwalPayRules.ValidOtp("123456\n")
                && !JawwalPayRules.ValidOtp("12 3456")
                && !JawwalPayRules.ValidOtp(new string('1', 33)),
            "OTP bounded and ASCII-only without whitespace"
        );

        handler.Body =
            "{\"ok\":true,\"session_key\":\"ps_secret_fixture\",\"amount\":25,\"expires_in\":900}";
        var session = await gateway.InitiateAsync(order, "0599123456", default);
        Check(
            session.Amount == 25 && session.ExpiresIn == 900,
            "parse documented initiate session"
        );
        Check(
            !session.ToString().Contains("ps_secret_fixture"),
            "session diagnostic does not expose secret"
        );
        Check(
            handler.Uri == "https://store.portal.ps/api/partner/pay/initiate"
                && handler.Method == "POST"
                && handler.Key == "fixture_partner_key_no_live_access",
            "pinned initiate endpoint and X-Partner-Key"
        );
        using (var body = JsonDocument.Parse(handler.RequestBody!))
        {
            Check(
                body.RootElement.GetProperty("amount").ValueKind == JsonValueKind.Number
                    && body.RootElement.GetProperty("amount").GetDecimal() == 25,
                "amount sent in shekels, never multiplied by 100"
            );
            Check(
                body.RootElement.GetProperty("external_ref").GetString() == external
                    && body.RootElement.GetProperty("phone").GetString() == "0599123456",
                "server reference and phone sent exactly"
            );
            Check(
                !handler.RequestBody!.Contains("fixture_partner_key"),
                "credential excluded from JSON body"
            );
        }
        string Receipt(string? reference = null, decimal amount = 25, long id = 12) =>
            JsonSerializer.Serialize(
                new
                {
                    ok = true,
                    transaction_id = id,
                    reference = "JW123456789",
                    external_ref = reference ?? external,
                    amount,
                    jawwal_fee = 1.5m,
                    platform_fee = 0,
                    net_credited = 23.5m,
                }
            );
        handler.Body = Receipt();
        var receipt = await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
        Check(
            receipt.TransactionId == 12 && receipt.Amount == 25 && receipt.NetCredited == 23.5m,
            "gross payment distinguished from partner net credit"
        );
        Check(
            handler.Uri == "https://store.portal.ps/api/partner/pay/confirm"
                && !handler.Uri.Contains("123456"),
            "confirmation is a POST, OTP never enters URL"
        );
        using (var body = JsonDocument.Parse(handler.RequestBody!))
            Check(
                body.RootElement.EnumerateObject().Count() == 2
                    && body.RootElement.GetProperty("session_key").GetString()
                        == "ps_secret_fixture"
                    && body.RootElement.GetProperty("otp").GetString() == "123456",
                "confirm sends only session_key and otp"
            );
        var numericStrings = System.Text.Json.Nodes.JsonNode.Parse(Receipt())!;
        foreach (
            var field in new[]
            {
                "transaction_id",
                "amount",
                "jawwal_fee",
                "platform_fee",
                "net_credited",
            }
        )
            numericStrings[field] = numericStrings[field]!.ToString();
        handler.Body = numericStrings.ToJsonString();
        var textReceipt = await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
        Check(
            textReceipt.TransactionId == 12
                && textReceipt.Amount == 25m
                && textReceipt.NetCredited == 23.5m,
            "String numeric receipt fields preserve gross amount, fees and transaction ID"
        );
        var optionalReferences = System.Text.Json.Nodes.JsonNode.Parse(Receipt())!;
        optionalReferences.AsObject().Remove("external_ref");
        optionalReferences.AsObject().Remove("reference");
        handler.Body = optionalReferences.ToJsonString();
        Check(
            (await gateway.ConfirmAsync(order, session.SessionKey, "123456", default)).TransactionId
                == 12,
            "Direct session-bound receipt permits omitted nullable reference fields"
        );
        foreach (
            var field in new[]
            {
                "transaction_id",
                "amount",
                "jawwal_fee",
                "platform_fee",
                "net_credited",
            }
        )
        {
            var missing = System.Text.Json.Nodes.JsonNode.Parse(Receipt())!;
            missing.AsObject().Remove(field);
            handler.Body = missing.ToJsonString();
            try
            {
                await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
                Check(false, "Missing financial field must reject");
            }
            catch (PaymentProviderException ex)
            {
                Check(
                    ex.ReasonCode == "WalletField." + field + ".Missing" && ex.HttpStatus == 200,
                    "Missing receipt field has a safe diagnostic code: " + field
                );
            }
        }
        var malformed = System.Text.Json.Nodes.JsonNode.Parse(Receipt())!;
        malformed["amount"] = "25,00";
        handler.Body = malformed.ToJsonString();
        try
        {
            await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
            Check(false, "Ambiguous amount must reject");
        }
        catch (PaymentProviderException ex)
        {
            Check(
                ex.ReasonCode == "WalletField.amount.InvalidString",
                "Ambiguous decimal string cannot confirm a debit"
            );
        }
        handler.Body =
            "{\"ok\":true,\"session_key\":\"ps_secret_fixture\",\"amount\":\"25.00\",\"expires_in\":\"900\"}";
        Check(
            (await gateway.InitiateAsync(order, "0599123456", default)).ExpiresIn == 900,
            "Numeric strings in OTP initialization are parsed without logging the session key"
        );
        foreach (
            var bad in new[]
            {
                Receipt("another-order"),
                Receipt(amount: 24),
                Receipt(id: 0),
                "{\"ok\":true}",
                "{\"ok\":false}",
            }
        )
        {
            handler.Body = bad;
            try
            {
                await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
                Check(false, "bad receipt rejected");
            }
            catch (PaymentProviderException ex)
            {
                Check(
                    !ex.ToString().Contains("ps_secret_fixture")
                        && !ex.ToString().Contains("123456"),
                    "mismatched or malformed receipt is never paid or logged"
                );
            }
        }
        foreach (
            var status in new[]
            {
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.ServiceUnavailable,
            }
        )
        {
            handler.Status = status;
            try
            {
                await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
                Check(false, "HTTP failure rejected");
            }
            catch (PaymentProviderException ex)
            {
                Check(
                    ex.ReasonCode == "WalletHttpFailure" && ex.HttpStatus == (int)status,
                    "HTTP "
                        + (int)status
                        + " cannot confirm debit and retains a safe diagnostic code"
                );
            }
        }
        handler.Status = HttpStatusCode.UnprocessableEntity;
        handler.Body = "{\"ok\":false,\"message\":\"untrusted details\"}";
        var before = handler.Calls;
        try
        {
            await gateway.ConfirmAsync(order, session.SessionKey, "123456", default);
            Check(false, "422 rejected");
        }
        catch (WalletRequestRejectedException)
        {
            Check(handler.Calls == before + 1, "422 returned as one failed attempt, no HTTP retry");
        }
        handler.Status = HttpStatusCode.OK;
        handler.Body =
            "{\"ok\":true,\"balance\":135,\"available\":135,\"gross_total\":250,\"jawwal_fees_total\":15,\"platform_fees_total\":0,\"credited_total\":235,\"withdrawn_total\":100,\"pending_withdrawals\":0,\"transactions_count\":10}";
        var balance = await gateway.BalanceAsync(default);
        Check(
            balance.Balance == 135
                && balance.GrossTotal == 250
                && handler.Method == "GET"
                && handler.Uri!.EndsWith("/api/partner/balance"),
            "balance parsed as a read-only query"
        );

        object Transaction(
            string status = "success",
            string? owner = null,
            string phone = "0599123456",
            long id = 12
        ) =>
            new
            {
                id,
                external_ref = owner ?? external,
                phone,
                amount = 25,
                jawwal_fee = 1.5m,
                platform_fee = 0,
                net_amount = 23.5m,
                status,
                jawwal_reference = "JW123456789",
            };
        WalletReceipt? Find(params object[] entries)
        {
            using var json = JsonDocument.Parse(
                JsonSerializer.Serialize(new { ok = true, transactions = entries })
            );
            return JawwalPayGateway.FindReceipt(json.RootElement, order, "0599123456");
        }
        Check(
            Find(Transaction())?.TransactionId == 12,
            "last-50 success matched by external_ref, phone and amount"
        );
        Check(
            Find() is null
                && Find(Transaction("pending")) is null
                && Find(Transaction("failed")) is null,
            "missing, pending and failed records never count as paid"
        );
        Check(
            Find(Transaction(owner: "different-order")) is null,
            "unrelated transactions cannot fund an order"
        );
        try
        {
            Find(Transaction(phone: "0599000000"));
            Check(false, "different phone rejected");
        }
        catch (PaymentProviderException)
        {
            Check(true, "different wallet phone rejected");
        }
        try
        {
            Find(Transaction(), Transaction(id: 13));
            Check(false, "duplicate debits rejected");
        }
        catch (PaymentProviderException)
        {
            Check(true, "multiple successful debits require review");
        }
        var entity = db.Model.FindEntityType(typeof(WalletPaymentSession))!;
        Check(
            entity
                .GetIndexes()
                .Any(x =>
                    x.IsUnique
                    && x.Properties.Any(property => property.Name == "TransactionId")
                    && x.Properties.Any(property => property.Name == "GatewayEnvironment")
                ),
            "one partner transaction cannot fund two orders"
        );
        Check(
            entity
                .GetIndexes()
                .Any(x =>
                    x.IsUnique
                    && x.Properties.Any(property => property.Name == "SessionKeyHash")
                    && x.Properties.Any(property => property.Name == "GatewayEnvironment")
                ),
            "one partner session cannot be reused across orders"
        );
        Check(entity.FindProperty("Otp") is null, "OTP is not a persisted database field");
        Check(
            PaymentTestEnvironment.Development.IsSandbox
                && !PaymentTestEnvironment.Production.IsSandbox,
            "Development selects sandbox and Production selects live profile"
        );
        Check(
            PaymentTestEnvironment.Staging.IsSandbox
                && !PaymentTestEnvironment.Staging.ShowDevelopmentHints,
            "non-Production hosts stay in sandbox but public test hints appear only in Development"
        );
        var sandboxOrder = new PaymentOrder
        {
            AmountIls = 25,
            Gateway = "JawwalPay",
            GatewayEnvironment = "Sandbox",
        };
        var sandbox = new JawwalPayGateway(
            new HttpClient(handler),
            configuration,
            db,
            PaymentTestEnvironment.Development
        );
        handler.Body =
            "{\"ok\":true,\"session_key\":\"test_fixture_session\",\"amount\":25,\"expires_in\":900}";
        await sandbox.InitiateAsync(sandboxOrder, "0599123456", default);
        Check(
            handler.Uri!.EndsWith("/api/partner/test/pay/initiate")
                && handler.Key == "fixture_sandbox_key_no_live_access",
            "sandbox initiation uses test path and separate sandbox credential"
        );
        handler.Body = Receipt(JawwalPayRules.ExternalReference(sandboxOrder.Id));
        await sandbox.ConfirmAsync(sandboxOrder, "test_fixture_session", "123456", default);
        Check(
            handler.Uri!.EndsWith("/api/partner/test/pay/confirm"),
            "sandbox confirmation never calls live confirm path"
        );
        var balanceCalls = handler.Calls;
        try
        {
            await sandbox.BalanceAsync(default);
            Check(false, "sandbox balance must not use live balance");
        }
        catch (PaymentProviderException)
        {
            Check(
                handler.Calls == balanceCalls,
                "sandbox balance cannot access production balance endpoint"
            );
        }
        configuration["Payments:Lahza:Sandbox:SecretKey"] = "sk_test_payment_probe_fixture";
        configuration["Payments:Lahza:Sandbox:PublicOrigin"] = "https://test.example";
        var lahzaHandler = new Handler
        {
            Body = "{\"status\":true,\"data\":{\"payment_session_timeout\":600}}",
        };
        var lahza = new LahzaGateway(
            new HttpClient(lahzaHandler),
            configuration,
            db,
            PaymentTestEnvironment.Development
        );
        handler.Body = "{\"ok\":true}";
        var probes = new PaymentToolProbes(lahza, sandbox);
        var walletProbe = await probes.CheckAsync("JawwalPayPayment", default);
        Check(
            walletProbe.State == ToolState.Ready
                && walletProbe.Detail == "Sandbox"
                && handler.Method == "GET"
                && handler.Uri!.EndsWith("/api/partner/test/ping"),
            "tools sandbox check only calls GET ping, without SMS or debit"
        );
        var lahzaProbe = await probes.CheckAsync("LahzaPayment", default);
        Check(
            lahzaProbe.State == ToolState.Ready
                && lahzaHandler.Method == "GET"
                && lahzaHandler.Uri!.EndsWith("/payment_session_timeout"),
            "tools Lahza check is read-only and does not create checkout"
        );
        try
        {
            await sandbox.InitiateAsync(order, "0599123456", default);
            Check(false, "cross-environment order must reject");
        }
        catch (PaymentProviderException)
        {
            Check(true, "production order cannot use sandbox wallet client");
        }
        var sql = db.MemberSubscriptions.ToQueryString();
        Check(
            sql.Contains("GatewayEnvironment") && sql.Contains("Production"),
            "production entitlement query includes environment isolation"
        );
        await using (
            var sandboxDb = new HiringDb(
                new DbContextOptionsBuilder<HiringDb>()
                    .UseSqlServer(
                        "Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"
                    )
                    .Options,
                PaymentTestEnvironment.Development
            )
        )
        {
            Check(
                sandboxDb.MemberSubscriptions.ToQueryString().Contains("Sandbox"),
                "cached EF model uses each context's current environment"
            );
        }
        configuration["Payments:JawwalPay:Production:ApiKey"] = "***";
        Check(!gateway.Ready, "placeholder leaves gateway unavailable");
        configuration["Payments:JawwalPay:Production:ApiKey"] =
            "fixture_partner_key_no_live_access";
        configuration["Payments:JawwalPay:ApiKeyHeader"] = "Authorization";
        Check(!gateway.Ready, "unconfirmed authentication header is not used");
        Console.WriteLine(
            $"Jawwal Pay checks passed: {count}. No SQL connection or live wallet request."
        );
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "{}";
        public HttpStatusCode Status = HttpStatusCode.OK;
        public int Calls;
        public string? Uri,
            Method,
            Key,
            RequestBody;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Calls++;
            Uri = request.RequestUri!.AbsoluteUri;
            Method = request.Method.Method;
            Key = request.Headers.TryGetValues("X-Partner-Key", out var values)
                ? values.Single()
                : request.Headers.Authorization?.Parameter;
            RequestBody =
                request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }
}
