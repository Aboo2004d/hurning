using System.Net;
using System.Text;
using HiringPs.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class MembershipSqlChecks
{
    // This always creates a uniquely named disposable database, never the supplied catalog.
    public static async Task RunAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("HIRING_TEST_SQL");
        if (string.IsNullOrWhiteSpace(supplied))
            throw new InvalidOperationException(
                "HIRING_TEST_SQL is required. No database was changed."
            );
        var connection = new SqlConnectionStringBuilder(supplied)
        {
            InitialCatalog = "HiringPs_MemberChecks_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<HiringDb>().UseSqlServer(connection).Options;
        await using var db = new HiringDb(options);
        var count = 0;
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            count++;
            Console.WriteLine("PASS: " + label);
        }
        try
        {
            await db.Database.MigrateAsync();
            Check(!db.Database.HasPendingModelChanges(), "SQL migration matches the model");
            var id = Guid.NewGuid().ToString();
            db.Users.Add(
                new IdentityUser
                {
                    Id = id,
                    UserName = "fixture@invalid.test",
                    NormalizedUserName = "FIXTURE@INVALID.TEST",
                    Email = "fixture@invalid.test",
                }
            );
            db.MemberProfiles.Add(
                new MemberProfile
                {
                    UserId = id,
                    Name = "Fixture",
                    Mobile = "+970599123456",
                    Activated = true,
                }
            );
            db.MemberPreferences.Add(new MemberPreference { UserId = id });
            var plan = new MembershipPlan();
            db.MembershipPlans.Add(plan);
            await db.SaveChangesAsync();
            var version = new MembershipPlanVersion
            {
                PlanId = plan.Id,
                NameAr = "تجربة",
                NameEn = "Fixture",
                PriceIls = 5,
                Days = 30,
                DetailKinds = MembershipRules.Bit(OpportunityKind.Job),
                AlertKinds = MembershipRules.Bit(OpportunityKind.Job),
            };
            db.MembershipPlanVersions.Add(version);
            await db.SaveChangesAsync();
            plan.CurrentVersionId = version.Id;
            var order = new PaymentOrder
            {
                UserId = id,
                PlanVersionId = version.Id,
                AmountIls = 5,
                Gateway = "fixture",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            db.PaymentOrders.Add(order);
            await db.SaveChangesAsync();
            async Task Confirm(Guid orderId)
            {
                await using var isolated = new HiringDb(options);
                await new MembershipService(isolated).ConfirmAsync(
                    orderId,
                    "Fixture",
                    false,
                    "Provider",
                    "Fixture",
                    "fixture:" + orderId,
                    5,
                    "ILS",
                    CancellationToken.None
                );
            }
            await Task.WhenAll(Confirm(order.Id), Confirm(order.Id));
            Check(
                await db.MemberSubscriptions.CountAsync(x => x.OrderId == order.Id) == 1,
                "Concurrent provider confirmations grant exactly one financial period"
            );
            var paid = await db.PaymentOrders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
            Check(
                paid.State == PaymentState.Paid,
                "Verified late payment is accepted after checkout expiry"
            );
            var first = await db
                .MemberSubscriptions.AsNoTracking()
                .SingleAsync(x => x.OrderId == order.Id);
            Check(
                first.EndsAt - first.StartsAt == TimeSpan.FromDays(30),
                "Purchased duration uses exact days"
            );
            var second = new PaymentOrder
            {
                UserId = id,
                PlanVersionId = version.Id,
                AmountIls = 5,
                Gateway = "fixture",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            db.PaymentOrders.Add(second);
            await db.SaveChangesAsync();
            await Confirm(second.Id);
            var renewal = await db
                .MemberSubscriptions.AsNoTracking()
                .SingleAsync(x => x.OrderId == second.Id);
            Check(
                renewal.StartsAt == first.EndsAt && renewal.EndsAt == first.EndsAt.AddDays(30),
                "Renewal preserves all remaining time"
            );
            var manualOrder = new PaymentOrder
            {
                UserId = id,
                PlanVersionId = version.Id,
                AmountIls = 5,
                Gateway = "fixture",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            db.PaymentOrders.Add(manualOrder);
            await db.SaveChangesAsync();
            await new MembershipService(db).ConfirmAsync(
                manualOrder.Id,
                "Fixture administrator",
                true,
                "Verified manually",
                "Fixture receipt",
                null,
                null,
                null,
                CancellationToken.None
            );
            await Confirm(manualOrder.Id);
            Check(
                await db.MemberSubscriptions.CountAsync(x => x.OrderId == manualOrder.Id) == 1
                    && await db
                        .PaymentOrders.AsNoTracking()
                        .AnyAsync(x => x.Id == manualOrder.Id && x.State == PaymentState.Paid),
                "Provider confirmation after manual approval reconciles without another period"
            );
            await db.OpportunityAccessRules.ExecuteUpdateAsync(x =>
                x.SetProperty(y => y.PublicDetails, false)
            );
            var service = new MembershipService(db);
            var rights = await service.DetailKindsAsync(id, false, CancellationToken.None);
            Check(
                rights.SetEquals([OpportunityKind.Job]),
                "Server grants only the purchased detail types"
            );
            await service.SuspendAsync(
                id,
                true,
                true,
                "Fixture freeze",
                "Fixture",
                CancellationToken.None
            );
            Check(
                (await service.DetailKindsAsync(id, false, CancellationToken.None)).Count == 0,
                "Suspension revokes member-only details"
            );
            db.ChangeTracker.Clear();
            await service.SuspendAsync(
                id,
                false,
                false,
                "Fixture resume",
                "Fixture",
                CancellationToken.None
            );
            var resumed = await db
                .MemberSubscriptions.AsNoTracking()
                .SingleAsync(x => x.OrderId == second.Id);
            Check(
                resumed.EndsAt > renewal.EndsAt,
                "Resume preserves frozen time for scheduled periods"
            );
            var item = new Opportunity
            {
                Kind = OpportunityKind.Job,
                Title = "Fixture publication",
                Status = PublicationStatus.Published,
            };
            db.Opportunities.Add(item);
            await db.SaveChangesAsync();
            Check(
                await db.OpportunityPublications.AnyAsync(x =>
                    x.OpportunityId == item.Id && !x.Completed
                ),
                "Publication marker is committed with a new generated opportunity ID"
            );
            var profile = await db.MemberProfiles.SingleAsync(x => x.UserId == id);
            profile.TelegramChatId = 123;
            profile.TelegramUserId = 123;
            db.MemberMessages.Add(
                new MemberMessage
                {
                    UserId = id,
                    EventKey = "review:fixture-only",
                    Kind = MemberMessageKind.Review,
                    Text = "Fixture only",
                }
            );
            await db.SaveChangesAsync();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Telegram:Enabled"] = "true",
                        ["Telegram:BotToken"] = "123:" + new string('x', 40),
                        ["Telegram:BotUsername"] = "fixture_bot",
                        ["Telegram:WebhookSecret"] = new string('s', 32),
                    }
                )
                .Build();
            await new MemberNotifications(
                db,
                new TelegramClient(new HttpClient(new FixtureTelegramHandler()), configuration),
                configuration
            ).DeliverAsync(CancellationToken.None);
            Check(
                await db
                    .MemberMessages.AsNoTracking()
                    .AnyAsync(x => x.EventKey == "review:fixture-only" && x.SentAt != null),
                "Outbox rowversion claim can deliver a message tracked in the same scope"
            );
            var frozenVersion = await db.MembershipPlanVersions.SingleAsync(x =>
                x.Id == version.Id
            );
            frozenVersion.NameAr = "attempted overwrite";
            var rejected = false;
            try
            {
                await db.SaveChangesAsync();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Check(rejected, "Purchased plan version cannot be overwritten");
            Console.WriteLine(
                $"Membership SQL checks passed: {count}. No Telegram or gateway requests."
            );
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FixtureTelegramHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
                }
            );
    }
}
