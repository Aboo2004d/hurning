using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>وحدة تتبع وتخزين بيانات الموقع؛ تفوض إعدادات كل كيان إلى ملف مستقل وتحمي ثبات النسخ المحفوظة.</summary>
public class HiringDb(
    DbContextOptions<HiringDb> options,
    PaymentEnvironment? paymentEnvironment = null
) : IdentityDbContext<IdentityUser>(options)
{
    /// <summary>بيئة الدفع المرتبطة بنسخة السياق الحالية؛ يستخدمها فلتر الاشتراكات لمنع خلط البيئات.</summary>
    public string ActivePaymentEnvironment => paymentEnvironment?.Name ?? "Production";
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SocialTemplateVersion"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SocialTemplateVersion> SocialTemplateVersions => Set<SocialTemplateVersion>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SocialConnection"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SocialConnection> SocialConnections => Set<SocialConnection>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SocialPublication"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SocialPublication> SocialPublications => Set<SocialPublication>();

    /// <summary>يتيح الاستعلام عن سجلات <see cref="Source"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<Source> Sources => Set<Source>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SourceItem"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SourceItem> SourceItems => Set<SourceItem>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SourceItemRevision"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SourceItemRevision> Revisions => Set<SourceItemRevision>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="Opportunity"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="OpportunitySourceLink"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<OpportunitySourceLink> SourceLinks => Set<OpportunitySourceLink>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="ScrapeRun"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<ScrapeRun> Runs => Set<ScrapeRun>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="UserReport"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<UserReport> Reports => Set<UserReport>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="AuditEntry"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="WorkerHeartbeat"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<WorkerHeartbeat> Heartbeats => Set<WorkerHeartbeat>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="ContactMessage"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SitePage"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SitePage> SitePages => Set<SitePage>();

    /// <summary>يتيح الاستعلام عن سجلات <see cref="PromptVersion"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="WorkflowSettings"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<WorkflowSettings> WorkflowSettings => Set<WorkflowSettings>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="AiUsage"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="DiscoveryCheckpoint"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<DiscoveryCheckpoint> DiscoveryCheckpoints => Set<DiscoveryCheckpoint>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="OpportunityDateRecord"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<OpportunityDateRecord> OpportunityDates => Set<OpportunityDateRecord>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MergeOperation"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MergeOperation> MergeOperations => Set<MergeOperation>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="RetentionPolicy"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();

    /// <summary>يتيح الاستعلام عن سجلات <see cref="ToolCheckRequest"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<ToolCheckRequest> ToolCheckRequests => Set<ToolCheckRequest>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="ToolCheckResult"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<ToolCheckResult> ToolCheckResults => Set<ToolCheckResult>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="ToolWorkerState"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<ToolWorkerState> ToolWorkers => Set<ToolWorkerState>();

    /// <summary>يتيح الاستعلام عن سجلات <see cref="MemberProfile"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MemberPreference"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MemberPreference> MemberPreferences => Set<MemberPreference>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="SavedOpportunity"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<SavedOpportunity> SavedOpportunities => Set<SavedOpportunity>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="OpportunityAccessRule"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<OpportunityAccessRule> OpportunityAccessRules => Set<OpportunityAccessRule>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MembershipPlan"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MembershipPlanVersion"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MembershipPlanVersion> MembershipPlanVersions => Set<MembershipPlanVersion>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MemberSubscription"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="DiscountCode"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="WalletPaymentSession"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<WalletPaymentSession> WalletPaymentSessions => Set<WalletPaymentSession>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="PaymentOrder"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="PaymentReview"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<PaymentReview> PaymentReviews => Set<PaymentReview>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MemberChallenge"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MemberChallenge> MemberChallenges => Set<MemberChallenge>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="MemberMessage"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MemberMessage> MemberMessages => Set<MemberMessage>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="OpportunityPublication"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<OpportunityPublication> OpportunityPublications => Set<OpportunityPublication>();

    /// <summary>يتيح الاستعلام عن سجلات <see cref="MembershipRuntimeState"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<MembershipRuntimeState> MembershipRuntimeStates => Set<MembershipRuntimeState>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="PaymentGatewaySetting"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<PaymentGatewaySetting> PaymentGatewaySettings => Set<PaymentGatewaySetting>();
    /// <summary>يتيح الاستعلام عن سجلات <see cref="TelegramInbox"/> وتتبع تغييراتها داخل وحدة العمل الحالية.</summary>
    public DbSet<TelegramInbox> TelegramInbox => Set<TelegramInbox>();

    /// <summary>يطبق إعدادات الكيانات الصريحة ثم يربط فلتر الدفع بنسخة السياق الحالية.</summary>
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfiguration(new PaymentOrderConfiguration());
        b.ApplyConfiguration(new MemberSubscriptionConfiguration());
        b.ApplyConfiguration(new WalletPaymentSessionConfiguration());
        b.ApplyConfiguration(new MembershipRuntimeStateConfiguration());
        b.ApplyConfiguration(new TelegramInboxConfiguration());
        b.ApplyConfiguration(new PaymentGatewaySettingConfiguration());
        b.ApplyConfiguration(new MemberProfileConfiguration());
        b.ApplyConfiguration(new MemberPreferenceConfiguration());
        b.ApplyConfiguration(new SavedOpportunityConfiguration());
        b.ApplyConfiguration(new OpportunityAccessRuleConfiguration());
        b.ApplyConfiguration(new MembershipPlanVersionConfiguration());
        b.ApplyConfiguration(new MembershipPlanConfiguration());
        b.ApplyConfiguration(new DiscountCodeConfiguration());
        b.ApplyConfiguration(new PaymentReviewConfiguration());
        b.ApplyConfiguration(new MemberChallengeConfiguration());
        b.ApplyConfiguration(new MemberMessageConfiguration());
        b.ApplyConfiguration(new OpportunityPublicationConfiguration());
        b.ApplyConfiguration(new SocialTemplateVersionConfiguration());
        b.ApplyConfiguration(new SocialPublicationConfiguration());
        b.ApplyConfiguration(new ToolCheckRequestConfiguration());
        b.ApplyConfiguration(new ToolCheckResultConfiguration());
        b.ApplyConfiguration(new ToolWorkerStateConfiguration());
        b.ApplyConfiguration(new DiscoveryCheckpointConfiguration());
        b.ApplyConfiguration(new PromptVersionConfiguration());
        b.ApplyConfiguration(new WorkflowSettingsConfiguration());
        b.ApplyConfiguration(new RetentionPolicyConfiguration());
        b.ApplyConfiguration(new AiUsageConfiguration());
        b.ApplyConfiguration(new OpportunityDateRecordConfiguration());
        b.ApplyConfiguration(new MergeOperationConfiguration());
        b.ApplyConfiguration(new ContactMessageConfiguration());
        b.ApplyConfiguration(new SourceItemConfiguration());
        b.ApplyConfiguration(new OpportunityConfiguration());
        b.ApplyConfiguration(new OpportunitySourceLinkConfiguration());
        b.ApplyConfiguration(new UserReportConfiguration());
        b.ApplyConfiguration(new SourceEntityConfiguration());
        // Keep the active environment bound to the current DbContext, not a cached configuration instance.
        b.Entity<MemberSubscription>()
            .HasQueryFilter(x => x.GatewayEnvironment == ActivePaymentEnvironment);
    }

    /// <summary>يحمي النسخ غير القابلة للتعديل ويسجل حدث النشر الأول ويطبع حقول البحث قبل الحفظ.</summary>
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

    /// <summary>يتحقق من قواعد ثبات البيانات ويطبعها ثم يحفظ التغييرات داخل المعاملة الحالية.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>يطبق قواعد الحفظ نفسها بصورة غير متزامنة مع احترام رمز الإلغاء.</summary>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        NormalizeRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
