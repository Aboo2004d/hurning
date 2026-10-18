using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HiringPs.Core;
using HiringPs.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class LahzaChecks
{
    private static int count;

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new Exception("FAIL: " + label);
        count++;
        Console.WriteLine("PASS: " + label);
    }

    public static async Task RunAsync()
    {
        const string secret = "sk_test_local_fixture_secret_do_not_use";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Payments:Lahza:Enabled"] = "true",
                    ["Payments:Lahza:Sandbox:SecretKey"] = secret,
                    ["Payments:Lahza:Sandbox:PublicOrigin"] = "https://test.example",
                }
            )
            .Build();
        await using var db = new HiringDb(
            new DbContextOptionsBuilder<HiringDb>()
                .UseSqlServer(
                    "Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"
                )
                .Options
        );
        var order = new PaymentOrder
        {
            AmountIls = 5m,
            Gateway = "Lahza",
            GatewayEnvironment = "Sandbox",
        };
        order.ProviderReference = LahzaGateway.Reference(order.Id);
        string Payload(
            string status = "success",
            int amount = 500,
            string currency = "ILS",
            string? domain = "test",
            string? reference = null
        ) =>
            JsonSerializer.Serialize(
                new
                {
                    status = true,
                    data = new
                    {
                        status,
                        amount,
                        currency,
                        domain,
                        reference = reference ?? order.ProviderReference,
                    },
                }
            );
        var handler = new FixtureHandler(Payload());
        var gateway = new LahzaGateway(
            new HttpClient(handler),
            config,
            db,
            PaymentTestEnvironment.Development
        );
        Check(gateway.Ready, "test gateway ready with complete configuration");
        var verified = await gateway.CheckAsync(order, default);
        Check(
            verified.Confirmed && verified.Amount == 5m && verified.Currency == "ILS",
            "500 agora equals 5 ILS"
        );
        Check(
            handler.Path == "https://api.lahza.io/transaction/verify/" + order.ProviderReference
                && handler.Authorization == "Bearer " + secret,
            "verification uses pinned server endpoint and bearer key"
        );
        var missingDomain = System.Text.Json.Nodes.JsonNode.Parse(Payload())!;
        missingDomain["data"]!.AsObject().Remove("domain");
        foreach (
            var payload in new[]
            {
                missingDomain.ToJsonString(),
                Payload(domain: null),
                Payload(domain: ""),
                Payload(domain: " "),
            }
        )
        {
            handler.Body = payload;
            Check(
                (await gateway.CheckAsync(order, default)).Confirmed,
                "Authenticated Sandbox verification accepts an omitted or empty optional domain"
            );
        }
        config["Payments:Lahza:Production:SecretKey"] = "sk_live_local_fixture_secret_do_not_use";
        config["Payments:Lahza:Production:PublicOrigin"] = "https://test.example";
        var production = new LahzaGateway(
            new HttpClient(handler),
            config,
            db,
            PaymentTestEnvironment.Production
        );
        order.GatewayEnvironment = "Production";
        handler.Body = missingDomain.ToJsonString();
        Check(
            (await production.CheckAsync(order, default)).Confirmed
                && handler.Authorization == "Bearer sk_live_local_fixture_secret_do_not_use",
            "Missing domain in Production still uses the selected live key"
        );
        handler.Body = Payload(domain: "test");
        try
        {
            await production.CheckAsync(order, default);
            Check(false, "Explicit test domain cannot confirm a Production payment");
        }
        catch (PaymentProviderException)
        {
            Check(true, "Explicit test domain is rejected in Production");
        }
        order.GatewayEnvironment = "Sandbox";
        foreach (var status in new[] { "pending", "failed", "abandoned", "unknown" })
        {
            handler.Body = Payload(status);
            Check(
                !(await gateway.CheckAsync(order, default)).Confirmed,
                "HTTP 200 with " + status + " never confirms payment"
            );
        }
        foreach (
            var payload in new[]
            {
                Payload(amount: 499),
                Payload(currency: "USD"),
                Payload(domain: "live"),
                Payload(domain: "unrecognized"),
                Payload().Replace("\"domain\":\"test\"", "\"domain\":42"),
                Payload(reference: "another-order"),
                "{\"status\":false}",
                "{\"status\":true,\"data\":{}}",
                "not json",
                "[]",
            }
        )
        {
            handler.Body = payload;
            try
            {
                await gateway.CheckAsync(order, default);
                Check(false, "mismatch must reject");
            }
            catch (PaymentProviderException ex)
            {
                Check(
                    !ex.ToString().Contains(secret),
                    "malformed/mismatched response rejects without disclosing secrets"
                );
            }
        }
        foreach (
            var failure in new[]
            {
                (Payload(amount: 499), "AmountMismatch"),
                (Payload(currency: "USD"), "CurrencyMismatch"),
                (Payload(domain: "live"), "ResponseEnvironmentMismatch"),
                (Payload(reference: "other-order"), "ReferenceMismatch"),
                ("not json", "MalformedJson"),
            }
        )
        {
            handler.Body = failure.Item1;
            try
            {
                await gateway.CheckAsync(order, default);
                Check(false, "Invalid response must reject");
            }
            catch (PaymentProviderException ex)
            {
                Check(
                    ex.ReasonCode == failure.Item2
                        && ex.HttpStatus == 200
                        && !ex.ToString().Contains(secret),
                    "Safe diagnostic reason and HTTP status identify " + failure.Item2
                );
            }
        }
        handler.Code = HttpStatusCode.Unauthorized;
        try
        {
            await gateway.CheckAsync(order, default);
            Check(false, "401 must reject");
        }
        catch (PaymentProviderException ex)
        {
            Check(
                ex.ReasonCode == "HttpFailure" && ex.HttpStatus == 401,
                "HTTP failure retains a safe status code"
            );
        }
        foreach (
            var uri in new[]
            {
                "http://checkout.lahza.io/a",
                "https://checkout.lahza.io.evil.example/a",
                "https://user@checkout.lahza.io/a",
                "https://checkout.lahza.io:444/a",
            }
        )
            Check(!LahzaGateway.IsCheckoutUrl(new Uri(uri)), "reject checkout URL " + uri);
        Check(
            LahzaGateway.IsCheckoutUrl(new Uri("https://checkout.lahza.io/abc")),
            "accept documented hosted checkout"
        );

        var bytes = Encoding.UTF8.GetBytes("{\"event\":\"refund.pending\",\"data\":{}}");
        var signature = Convert
            .ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes))
            .ToLowerInvariant();
        Check(
            LahzaGateway.VerifySignature(
                Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog"),
                "f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8",
                "key"
            ),
            "known independent HMAC-SHA256 vector"
        );
        Check(
            LahzaGateway.VerifySignature(bytes, signature, secret),
            "accept SHA256 hex signature on original bytes"
        );
        Check(
            !LahzaGateway.VerifySignature(Encoding.UTF8.GetBytes("changed"), signature, secret),
            "reject modified webhook body"
        );
        Check(
            !LahzaGateway.VerifySignature(bytes, new string('z', 64), secret),
            "reject invalid hex signature"
        );
        Check(
            !LahzaGateway.VerifySignature(bytes, signature, "***"),
            "placeholders never authenticate"
        );
        Check(
            !LahzaGateway.VerifySignature(
                bytes,
                Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), bytes)),
                secret
            ),
            "reject erroneous SHA512 documentation example"
        );

        LahzaController Controller(byte[] body, string header)
        {
            var controller = new LahzaController(db, config, gateway);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };
            controller.Request.Body = new System.IO.MemoryStream(body);
            controller.Request.Headers["x-lahza-signature"] = header;
            return controller;
        }
        Check(
            await Controller(bytes, signature).Webhook(default) is OkResult,
            "signed unrelated event acknowledged without payment processing"
        );
        Check(
            await Controller(bytes, "wrong").Webhook(default) is UnauthorizedResult,
            "unsigned event rejected before SQL access"
        );
        var invalid = Encoding.UTF8.GetBytes("not json");
        var invalidSignature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), invalid)
        );
        Check(
            await Controller(invalid, invalidSignature).Webhook(default) is BadRequestResult,
            "signed malformed JSON rejected"
        );
        foreach (var lang in new[] { "ar", "en" })
        {
            var result = (LocalRedirectResult)
                Controller(bytes, signature).Returned(order.ProviderReference, lang);
            Check(
                result.Url == "/" + lang + "/Member/Payments/Returned/" + order.Id,
                "callback preserves " + lang + " and only navigates"
            );
        }
        Check(
            Controller(bytes, signature).Returned("untrusted", "en") is BadRequestResult,
            "malformed callback reference rejected"
        );
        config["Payments:Lahza:Sandbox:SecretKey"] = "***";
        Check(!gateway.Ready, "placeholder keeps adapter unavailable");
        await PaymentTraceChecks.RunAsync();
        Console.WriteLine($"Lahza checks passed: {count}");
    }

    private sealed class FixtureHandler(string body) : HttpMessageHandler
    {
        public string Body = body;
        public HttpStatusCode Code = HttpStatusCode.OK;
        public string? Path;
        public string? Authorization;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Path = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization!.ToString();
            return Task.FromResult(
                new HttpResponseMessage(Code) { Content = new StringContent(Body) }
            );
        }
    }
}
