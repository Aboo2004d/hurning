// تعريف العلاقات والفهارس وتسجيل خدمات النظام في حاوية الاعتماديات.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

public class HiringDb(
    DbContextOptions<HiringDb> options,
    PaymentEnvironment? paymentEnvironment = null
) : IdentityDbContext<IdentityUser>(options)
{
    public string ActivePaymentEnvironment => paymentEnvironment?.Name ?? "Production";
    public DbSet<SocialTemplateVersion> SocialTemplateVersions => Set<SocialTemplateVersion>();
    public DbSet<SocialConnection> SocialConnections => Set<SocialConnection>();
    public DbSet<SocialPublication> SocialPublications => Set<SocialPublication>();

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceItem> SourceItems => Set<SourceItem>();
    public DbSet<SourceItemRevision> Revisions => Set<SourceItemRevision>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunitySourceLink> SourceLinks => Set<OpportunitySourceLink>();
    public DbSet<ScrapeRun> Runs => Set<ScrapeRun>();
    public DbSet<UserReport> Reports => Set<UserReport>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<WorkerHeartbeat> Heartbeats => Set<WorkerHeartbeat>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<SitePage> SitePages => Set<SitePage>();

    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    public DbSet<WorkflowSettings> WorkflowSettings => Set<WorkflowSettings>();
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    public DbSet<DiscoveryCheckpoint> DiscoveryCheckpoints => Set<DiscoveryCheckpoint>();
    public DbSet<OpportunityDateRecord> OpportunityDates => Set<OpportunityDateRecord>();
    public DbSet<MergeOperation> MergeOperations => Set<MergeOperation>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();

    public DbSet<ToolCheckRequest> ToolCheckRequests => Set<ToolCheckRequest>();
    public DbSet<ToolCheckResult> ToolCheckResults => Set<ToolCheckResult>();
    public DbSet<ToolWorkerState> ToolWorkers => Set<ToolWorkerState>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<MemberPreference> MemberPreferences => Set<MemberPreference>();
    public DbSet<SavedOpportunity> SavedOpportunities => Set<SavedOpportunity>();
    public DbSet<OpportunityAccessRule> OpportunityAccessRules => Set<OpportunityAccessRule>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MembershipPlanVersion> MembershipPlanVersions => Set<MembershipPlanVersion>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<WalletPaymentSession> WalletPaymentSessions => Set<WalletPaymentSession>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<PaymentReview> PaymentReviews => Set<PaymentReview>();
    public DbSet<MemberChallenge> MemberChallenges => Set<MemberChallenge>();
    public DbSet<MemberMessage> MemberMessages => Set<MemberMessage>();
    public DbSet<OpportunityPublication> OpportunityPublications => Set<OpportunityPublication>();

    public DbSet<MembershipRuntimeState> MembershipRuntimeStates => Set<MembershipRuntimeState>();
    public DbSet<PaymentGatewaySetting> PaymentGatewaySettings => Set<PaymentGatewaySetting>();
    public DbSet<TelegramInbox> TelegramInbox => Set<TelegramInbox>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<SocialTemplateVersion>().HasIndex(x => new { x.Provider, x.Scope, x.Number }).IsUnique();
        b.Entity<SocialTemplateVersion>().HasIndex(x => new { x.Provider, x.Scope }).IsUnique().HasFilter("[Active] = 1");
        b.Entity<SocialPublication>().HasOne<SocialTemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SocialPublication>().HasIndex(x => new { x.OpportunityId, x.Provider, x.CreatedAt });
        b.Entity<SocialPublication>().HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        MembershipSchema.Configure(b);
        b.Entity<MemberSubscription>()
            .HasQueryFilter(x => x.GatewayEnvironment == ActivePaymentEnvironment);
        b.Entity<ToolCheckRequest>()
            .HasIndex(x => x.ActiveSlot)
            .IsUnique()
            .HasFilter("[ActiveSlot] IS NOT NULL");
        b.Entity<ToolCheckRequest>().HasIndex(x => new { x.Kind, x.RequestedAt });
        b.Entity<ToolCheckResult>().HasIndex(x => new { x.Tool, x.CheckedAt });
        b.Entity<ToolCheckResult>()
            .HasOne<ToolCheckRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ToolWorkerState>().HasIndex(x => x.At);
        // A checkpoint is keyed by its existing source, not a generated identity.
        b.Entity<DiscoveryCheckpoint>().Property(x => x.SourceId).ValueGeneratedNever();
        b.Entity<PromptVersion>().HasData(PromptDefaults.Initial());
        b.Entity<WorkflowSettings>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<WorkflowSettings>()
            .HasData(new WorkflowSettings { Id = 1, ActivePromptVersionId = 1 });
        b.Entity<RetentionPolicy>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<RetentionPolicy>().HasData(new RetentionPolicy { Id = 1 });
        b.Entity<WorkflowSettings>()
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.ActivePromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkflowSettings>()
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.PendingPromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiUsage>()
            .HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(x => x.PromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiUsage>()
            .HasIndex(x => new
            {
                x.StartedAt,
                x.PromptVersionId,
                x.Model,
            });
        b.Entity<OpportunityDateRecord>()
            .HasOne(x => x.Opportunity)
            .WithMany()
            .HasForeignKey(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpportunityDateRecord>()
            .HasIndex(x => new
            {
                x.OpportunityId,
                x.Kind,
                x.EvidenceHash,
            })
            .IsUnique();
        b.Entity<MergeOperation>()
            .HasIndex(x => new
            {
                x.DuplicateId,
                x.TargetId,
                x.UndoneAt,
            });

        b.Entity<ContactMessage>()
            .HasIndex(x => new
            {
                x.IsHandled,
                x.IsRead,
                x.CreatedAt,
            });
        b.Entity<SourceItem>().HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();
        b.Entity<SourceItem>().HasIndex(x => new { x.Status, x.RetryAt });
        b.Entity<Opportunity>()
            .HasIndex(x => new
            {
                x.Status,
                x.Kind,
                x.Deadline,
            });
        b.Entity<Opportunity>().HasIndex(x => x.Fingerprint);
        b.Entity<Opportunity>().HasIndex(x => new { x.Kind, x.NormalizedOrganization });
        b.Entity<OpportunitySourceLink>()
            .HasIndex(x => new { x.SourceItemId, x.ChildKey })
            .IsUnique();
        b.Entity<OpportunitySourceLink>()
            .HasOne(x => x.SourceItem)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserReport>()
            .HasOne(x => x.Opportunity)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Source>()
            .ToTable(t =>
                t.HasCheckConstraint("CK_Source_Interval", "[IntervalHours] BETWEEN 1 AND 168")
            );
    }

    private void NormalizeRecords()
    {
        if (ChangeTracker.Entries<SocialTemplateVersion>().Any(x => x.State == EntityState.Deleted
            || (x.State == EntityState.Modified && x.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(SocialTemplateVersion.Active)))))
            throw new InvalidOperationException("Social template versions are immutable; create a new version.");

        if (
            ChangeTracker
                .Entries<PromptVersion>()
                .Any(x => x.State is EntityState.Modified or EntityState.Deleted)
        )
            throw new InvalidOperationException(
                "Prompt versions are immutable; create a new version instead."
            );

        if (
            ChangeTracker
                .Entries<MembershipPlanVersion>()
                .Any(x => x.State is EntityState.Modified or EntityState.Deleted)
        )
            throw new InvalidOperationException(
                "Purchased plan versions are immutable; create a new version."
            );

        // Capture only a first publication. Existing records are not backfilled on deployment.
        foreach (
            var entry in ChangeTracker
                .Entries<Opportunity>()
                .Where(x =>
                    x.Entity.Status == PublicationStatus.Published
                    && (
                        x.State == EntityState.Added
                        || (
                            x.State == EntityState.Modified
                            && x.Property(y => y.Status).OriginalValue
                                != PublicationStatus.Published
                        )
                    )
                )
                .ToList()
        )
        {
            if (
                (
                    entry.Entity.Id == 0
                    || !OpportunityPublications.Local.Any(x => x.OpportunityId == entry.Entity.Id)
                )
                && (
                    entry.Entity.Id == 0
                    || !OpportunityPublications.Any(x => x.OpportunityId == entry.Entity.Id)
                )
            )
                OpportunityPublications.Add(
                    new OpportunityPublication
                    {
                        Opportunity = entry.Entity,
                        PublishedAt = DateTimeOffset.UtcNow,
                    }
                );
        }

        foreach (
            var entry in ChangeTracker
                .Entries<Opportunity>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
        )
        {
            entry.Entity.NormalizedOrganization = TextRules.OrganizationKey(
                entry.Entity.Organization
            );
            entry.Entity.LocationSearchText = LocationSearch.Index(entry.Entity.Locations);
            entry.Entity.SearchText = LocationSearch.Normalize(
                $"{entry.Entity.Title} {entry.Entity.Organization} {entry.Entity.Summary} {entry.Entity.Category} {entry.Entity.LocationSearchText}"
            );
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        NormalizeRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
