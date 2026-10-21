using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class PreferenceAndTelegramComposeChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool valid, string description)
        {
            if (!valid) throw new Exception(description);
            passed++;
        }
        Check(PreferenceLimitRules.Effective([]) is null, "No subscription preserves preparation of preferences.");
        Check(PreferenceLimitRules.Effective([2, 5]) == 5, "Concurrent subscriptions use the most generous cap.");
        Check(PreferenceLimitRules.Effective([2, null]) is null, "Unlimited subscription remains unlimited.");
        Check(PreferenceLimitRules.Count([" IT ", "it", "", "Accounting"]) == 2, "Duplicates and blanks do not consume slots.");
        Check(!PreferenceLimitRules.AllowsAlerts([], 3), "Empty capped preferences cannot bypass the quota as all categories.");
        Check(!PreferenceLimitRules.AllowsAlerts(["A", "B"], 1), "Old excess preferences cannot bypass a smaller active quota.");
        Check(PreferenceLimitRules.AllowsAlerts(["A", "B"], 2), "The exact cap permits alerts.");
        Check(PreferenceLimitRules.AllowsAlerts([], null), "Unlimited subscriptions retain wildcard semantics.");
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ShapeOnly;Trusted_Connection=True").Options);
        Check(!db.Database.HasPendingModelChanges(), "Migration snapshot matches the new columns.");
        var projected = MembershipPlanProjection.Summary.Compile()(new() { MaxPreferredCategories = 3 });
        Check(projected.MaxPreferredCategories == 3, "Catalog and checkout DTOs preserve the purchased cap.");
        var manager = new PlanManagementService(db, new MembershipService(db));
        foreach (var limit in new[] { 0, -1, 1001 })
        {
            var outcome = await manager.SaveAsync(new(null, "باقة", "Plan", "", "", "", "", 5, 30,
                [], [], true, 0, null, "Test", limit), CancellationToken.None);
            Check(outcome.Outcome == PlanSaveOutcome.InvalidRequest, "Invalid plan limits are rejected before database writes.");
        }
        foreach (var kind in Enum.GetValues<OpportunityKind>())
        {
            var targets = TelegramChannelTargets.ForOpportunity(kind);
            Check(targets.Length == 2 && targets[1] == "All"
                && TelegramChannelTargets.Kinds(targets[0]).Contains(kind), "Every kind has only its own channel and the general channel.");
        }
        Check(TelegramChannelTargets.ForOpportunity((OpportunityKind)999).Length == 0, "Unknown kinds expose no channels.");
        var publication = db.Model.FindEntityType(typeof(SocialPublication))!;
        Check(publication.GetIndexes().Any(x => x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "OpportunityId", "Provider", "PageId" })),
            "Manual and automatic channel posts share the same duplicate protection.");
        Check(!new SocialPublication().ManualCompose && new MembershipPlanVersion().MaxPreferredCategories is null,
            "Existing automatic posts and unlimited plans retain their defaults.");
        var service = new TelegramComposeService(db, null!, null!, new ConfigurationBuilder().Build());
        foreach (var command in new[]
        {
            new TelegramComposeCommandDto(Guid.Empty, 42, null, "Text", ["Jobs"], "Test"),
            new TelegramComposeCommandDto(Guid.NewGuid(), 42, null, "", ["Jobs"], "Test"),
            new TelegramComposeCommandDto(Guid.NewGuid(), 42, null, "Text", [], "Test"),
            new TelegramComposeCommandDto(Guid.NewGuid(), 42, null, new string('a', 4097), ["Jobs"], "Test"),
        })
            Check(await service.QueueAsync(command, CancellationToken.None) == TelegramComposeOutcome.Invalid,
                "Invalid manual publish requests are rejected without database or provider access.");
        Console.WriteLine($"PASS: {passed} plan preference limit and Telegram compose checks; no database writes or external requests.");
    }
}
