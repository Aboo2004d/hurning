using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Runs only inside MembershipSqlChecks' disposable SQL database. All partner HTTP
// responses are supplied in memory, including a reset after a simulated debit.
public static class WalletSqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options, int versionId)
    {
        var configuration = JawwalPayChecks.Configuration();
        var protection = new EphemeralDataProtectionProvider();
        var transport = new PartnerHandler();
        var checks = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception("FAIL: " + message);
            checks++;
            Console.WriteLine("PASS: " + message);
        }
        await using (var setup = new HiringDb(options))
            await setup
                .PaymentGatewaySettings.Where(x => x.Key == "JawwalPay")
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Enabled, true));

        async Task<PaymentOrder> Order(string environment = "Production")
        {
            await using var db = new HiringDb(options);
            var owner = Guid.NewGuid().ToString();
            db.Users.Add(new IdentityUser { Id = owner, UserName = owner + "@invalid.test" });
            db.MemberProfiles.Add(
                new MemberProfile
                {
                    UserId = owner,
                    Name = "Wallet fixture",
                    Activated = true,
                    Mobile = "+970599123456",
                }
            );
            var order = new PaymentOrder
            {
                UserId = owner,
                PlanVersionId = versionId,
                Gateway = "JawwalPay",
                AmountIls = 5,
                GatewayEnvironment = environment,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            db.PaymentOrders.Add(order);
            await db.SaveChangesAsync();
            return order;
        }
        async Task<string> Initiate(PaymentOrder order, string? owner = null)
        {
            await using var db = new HiringDb(options);
            var membership = new MembershipService(db);
            var gateway = new JawwalPayGateway(new HttpClient(transport, false), configuration, db);
            var registry = new PaymentGatewayRegistry(db, configuration, [gateway]);
            return await new WalletCheckoutService(
                db,
                membership,
                registry,
                gateway,
                protection
            ).InitiateAsync(order.Id, owner ?? order.UserId, "0599123456", default);
        }
        async Task<string> Confirm(PaymentOrder order, string code)
        {
            await using var db = new HiringDb(options);
            var membership = new MembershipService(db);
            var gateway = new JawwalPayGateway(new HttpClient(transport, false), configuration, db);
            var registry = new PaymentGatewayRegistry(db, configuration, [gateway]);
            return await new WalletCheckoutService(
                db,
                membership,
                registry,
                gateway,
                protection
            ).ConfirmAsync(order.Id, order.UserId, code, default);
        }
        async Task<string> Cancel(PaymentOrder order, string? owner = null)
        {
            await using var db = new HiringDb(options);
            var membership = new MembershipService(db);
            var gateway = new JawwalPayGateway(new HttpClient(transport, false), configuration, db);
            var registry = new PaymentGatewayRegistry(db, configuration, [gateway]);
            return await new WalletCheckoutService(
                db,
                membership,
                registry,
                gateway,
                protection
            ).CancelAsync(order.Id, owner ?? order.UserId, default);
        }
        var cancelled = await Order();
        await Initiate(cancelled);
        var debitCalls = transport.Confirmations;
        Check(
            await Cancel(cancelled, "another-member") == "PaymentNotFound",
            "Wallet cancellation enforces order ownership"
        );
        Check(
            await Cancel(cancelled) == "WalletCancelled",
            "Wallet cancellation invalidates an unconfirmed OTP session"
        );
        await using (var db = new HiringDb(options))
        {
            var session = await db.WalletPaymentSessions.SingleAsync(x =>
                x.OrderId == cancelled.Id
            );
            var order = await db.PaymentOrders.SingleAsync(x => x.Id == cancelled.Id);
            Check(
                order.State == PaymentState.Cancelled
                    && order.ExpiresAt <= DateTimeOffset.UtcNow
                    && session.ProtectedSessionKey is null
                    && session.ExpiresAt <= DateTimeOffset.UtcNow,
                "Cancelled order expires and erases its encrypted session key without deleting the audit trail"
            );
        }
        await Confirm(cancelled, "123456");
        Check(transport.Confirmations == debitCalls, "Cancelled OTP cannot send a debit request");
        Check(
            await Initiate(cancelled) == "WalletCancelled",
            "Cancelled order cannot open another OTP session"
        );

        var initialInitiations = transport.Initiations;
        var first = await Order();
        var requested = await Task.WhenAll(Initiate(first), Initiate(first));
        Check(
            transport.Initiations == initialInitiations + 1 && requested.Contains("WalletOtpSent"),
            "concurrent OTP requests initialize one partner session"
        );
        await using (var db = new HiringDb(options))
        {
            var saved = await db
                .WalletPaymentSessions.AsNoTracking()
                .SingleAsync(x => x.OrderId == first.Id);
            Check(
                saved.ProtectedSessionKey != null
                    && !saved.ProtectedSessionKey.Contains("ps_sql_fixture"),
                "persisted provider session is encrypted"
            );
            Check(
                saved.ExpiresAt <= first.ExpiresAt,
                "900-second partner session cannot extend the site's 10-minute order"
            );
        }
        var confirmed = await Task.WhenAll(Confirm(first, "123456"), Confirm(first, "123456"));
        Check(
            transport.Confirmations == 1 && confirmed.Contains("PaymentConfirmed"),
            "concurrent confirmations send exactly one debit request"
        );
        Check(
            await Confirm(first, "123456") == "PaymentConfirmed" && transport.Confirmations == 1,
            "paid browser replay does not call confirm again"
        );
        await using (var db = new HiringDb(options))
        {
            Check(
                await db.MemberSubscriptions.CountAsync(x => x.OrderId == first.Id) == 1,
                "one wallet order creates one subscription period"
            );
            var saved = await db
                .WalletPaymentSessions.AsNoTracking()
                .SingleAsync(x => x.OrderId == first.Id);
            Check(
                saved.ConfirmedAmount == 5 && saved.NetCredited == 4.7m,
                "gross amount and merchant net are stored separately"
            );
        }
        var rejected = await Order();
        await Initiate(rejected);
        Check(
            await Confirm(rejected, "000000") == "WalletConfirmRejected",
            "422 reports a rejected confirmation"
        );
        var before = transport.Confirmations;
        Check(
            await Confirm(rejected, "123456") == "WalletAlreadyConfirming"
                && transport.Confirmations == before,
            "even a rejected code does not reuse the provider session for a second confirm"
        );
        var interrupted = await Order();
        await Initiate(interrupted);
        Check(
            await Confirm(interrupted, "999999") == "PaymentUncertain",
            "reset after debit remains unresolved rather than retried"
        );
        before = transport.Confirmations;
        Check(
            await Cancel(interrupted) == "WalletCancellationUnavailable",
            "An uncertain debit cannot be cancelled or made retryable"
        );
        await using (var db = new HiringDb(options))
        {
            await db
                .PaymentOrders.Where(x => x.Id == interrupted.Id)
                .ExecuteUpdateAsync(update =>
                    update.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))
                );
            var membership = new MembershipService(db);
            var gateway = new JawwalPayGateway(new HttpClient(transport, false), configuration, db);
            var registry = new PaymentGatewayRegistry(db, configuration, [gateway]);
            var result = await new PaymentReconciliation(db, membership, registry).CheckAsync(
                interrupted.Id,
                interrupted.UserId,
                "Wallet SQL fixture",
                default
            );
            Check(
                result == "PaymentConfirmed" && transport.Confirmations == before,
                "expired order recovers paid status using GET transactions, without another debit"
            );
            Check(
                await db.MemberSubscriptions.CountAsync(x => x.OrderId == interrupted.Id) == 1,
                "recovered late charge grants once"
            );
        }
        Check(
            await Initiate(await Order(), "another-member") == "PaymentNotFound",
            "wallet initiation rejects another member's order"
        );
        var sandbox = await Order("Sandbox");
        await using (var db = new HiringDb(options, PaymentTestEnvironment.Development))
        {
            await new MembershipService(db).ConfirmAsync(
                sandbox.Id,
                "Fixture",
                false,
                "Sandbox receipt",
                "Fixture",
                "sandbox-fixture:" + sandbox.Id,
                5,
                "ILS",
                default
            );
            Check(
                await db.MemberSubscriptions.AnyAsync(x => x.OrderId == sandbox.Id),
                "sandbox subscription is visible to Development context"
            );
        }
        await using (var db = new HiringDb(options))
        {
            Check(
                !await db.MemberSubscriptions.AnyAsync(x => x.OrderId == sandbox.Id)
                    && await db
                        .MemberSubscriptions.IgnoreQueryFilters()
                        .AnyAsync(x => x.OrderId == sandbox.Id),
                "production entitlements exclude sandbox subscriptions without deleting history"
            );
        }
        Console.WriteLine(
            $"Wallet SQL checks passed: {checks}. Partner HTTP was simulated; no live debit or SMS."
        );
    }

    private sealed record Charge(long Id, string External, string Phone, decimal Amount);

    private sealed class PartnerHandler : HttpMessageHandler
    {
        public int Initiations,
            Confirmations;
        private long nextId = 10000;
        private readonly ConcurrentDictionary<string, Charge> sessions = new();
        private readonly ConcurrentDictionary<long, Charge> paid = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/initiate"))
            {
                Interlocked.Increment(ref Initiations);
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var root = json.RootElement;
                var charge = new Charge(
                    Interlocked.Increment(ref nextId),
                    root.GetProperty("external_ref").GetString()!,
                    root.GetProperty("phone").GetString()!,
                    root.GetProperty("amount").GetDecimal()
                );
                var key = "ps_sql_fixture_" + charge.Id;
                sessions[key] = charge;
                await Task.Delay(25, ct);
                return Reply(
                    new
                    {
                        ok = true,
                        session_key = key,
                        amount = charge.Amount,
                        expires_in = 900,
                    }
                );
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/confirm"))
            {
                Interlocked.Increment(ref Confirmations);
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var root = json.RootElement;
                if (root.GetProperty("otp").GetString() == "000000")
                    return Reply(new { ok = false }, HttpStatusCode.UnprocessableEntity);
                var charge = sessions[root.GetProperty("session_key").GetString()!];
                paid[charge.Id] = charge;
                if (root.GetProperty("otp").GetString() == "999999")
                    throw new HttpRequestException("Simulated connection reset");
                return Reply(
                    new
                    {
                        ok = true,
                        transaction_id = charge.Id,
                        external_ref = charge.External,
                        reference = "JW" + charge.Id,
                        amount = charge.Amount,
                        jawwal_fee = charge.Amount * .06m,
                        platform_fee = 0,
                        net_credited = charge.Amount * .94m,
                    }
                );
            }
            if (
                request.Method == HttpMethod.Get
                && request.RequestUri.AbsolutePath.EndsWith("/transactions")
            )
                return Reply(
                    new
                    {
                        ok = true,
                        transactions = paid.Values.Select(charge => new
                        {
                            id = charge.Id,
                            external_ref = charge.External,
                            phone = charge.Phone,
                            amount = charge.Amount,
                            jawwal_fee = charge.Amount * .06m,
                            platform_fee = 0,
                            net_amount = charge.Amount * .94m,
                            status = "success",
                            jawwal_reference = "JW" + charge.Id,
                        }),
                    }
                );
            throw new Exception("Unexpected partner request");
        }

        private static HttpResponseMessage Reply(
            object data,
            HttpStatusCode status = HttpStatusCode.OK
        ) => new(status) { Content = new StringContent(JsonSerializer.Serialize(data)) };
    }
}
