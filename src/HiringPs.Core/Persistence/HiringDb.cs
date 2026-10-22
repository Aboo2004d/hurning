// تعريف العلاقات والفهارس وتسجيل خدمات النظام في حاوية الاعتماديات.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>مكون HiringDb ضمن مجال HiringPs.Core؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public class HiringDb(
    DbContextOptions<HiringDb> options,
    PaymentEnvironment? paymentEnvironment = null
) : IdentityDbContext<IdentityUser>(options)
{
    private readonly OperationTracking operations = new();

    public string ActivePaymentEnvironment => paymentEnvironment?.Name ?? "Production";
    public DbSet<SocialTemplateVersion> SocialTemplateVersions => Set<SocialTemplateVersion>();
    public DbSet<SocialConnection> SocialConnections => Set<SocialConnection>();
    public DbSet<SocialPublication> SocialPublications => Set<SocialPublication>();
    public DbSet<TelegramChannelSetting> TelegramChannelSettings => Set<TelegramChannelSetting>();
    public DbSet<IndexNowEntry> IndexNowEntries => Set<IndexNowEntry>();

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceItem> SourceItems => Set<SourceItem>();
    public DbSet<SourceItemRevision> Revisions => Set<SourceItemRevision>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunitySourceLink> SourceLinks => Set<OpportunitySourceLink>();
    public DbSet<ScrapeRun> Runs => Set<ScrapeRun>();
    public DbSet<UserReport> Reports => Set<UserReport>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<ProtectionKeyRecord> ProtectionKeys => Set<ProtectionKeyRecord>();
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();
    public DbSet<CategoryGroupMember> CategoryGroupMembers => Set<CategoryGroupMember>();
    public DbSet<WorkerHeartbeat> Heartbeats => Set<WorkerHeartbeat>();
    public DbSet<PipelineState> PipelineStates => Set<PipelineState>();
    public DbSet<OrganizationAlias> OrganizationAliases => Set<OrganizationAlias>();
    public DbSet<DuplicateCandidate> DuplicateCandidates => Set<DuplicateCandidate>();
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

    /// <summary>ينفذ OnModelCreating وفق قواعد HiringDb الحالية دون تغيير عقد المستدعين.</summary>
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<CategoryGroup>().Property(x => x.NormalizedName).UseCollation("Latin1_General_100_BIN2");
        b.Entity<CategoryGroupMember>().Property(x => x.NormalizedKey).UseCollation("Latin1_General_100_BIN2");
        b.ApplyConfiguration(new AuditEntryConfiguration());
        b.ApplyConfiguration(new SocialTemplateVersionConfiguration());
        b.ApplyConfiguration(new SocialPublicationConfiguration());
        b.ApplyConfiguration(new IndexNowEntryConfiguration());
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
        b.ApplyConfiguration(new SourceItemRevisionConfiguration());
        b.ApplyConfiguration(new ScrapeRunConfiguration());
        b.ApplyConfiguration(new PipelineStateConfiguration());
        b.ApplyConfiguration(new OrganizationAliasConfiguration());
        b.ApplyConfiguration(new DuplicateCandidateConfiguration());
        b.ApplyConfiguration(new OpportunityConfiguration());
        b.ApplyConfiguration(new OpportunitySourceLinkConfiguration());
        b.ApplyConfiguration(new UserReportConfiguration());
        b.ApplyConfiguration(new SourceEntityConfiguration());
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
        // Environment belongs to this DbContext instance, not to a cached configuration.
        b.Entity<MemberSubscription>().HasQueryFilter(x => x.GatewayEnvironment == ActivePaymentEnvironment);
    }

    /// <summary>يحمي النسخ الثابتة ويحدث فهارس البحث وطوابير أول نشر والفهرسة.</summary>
    private void NormalizeRecords() =>
        NormalizeRecords(ids =>
            OpportunityPublications
                .Where(x => ids.Contains(x.OpportunityId))
                .Select(x => x.OpportunityId)
                .ToList()
        );

    /// <summary>يحمل سجلات النشر دفعة واحدة قبل تطبيق التطبيع وقواعد الثبات.</summary>
    private async Task NormalizeRecordsAsync(CancellationToken ct)
    {
        var ids = SavedIds(NewlyPublished());
        var recorded =
            ids.Length == 0
                ? []
                : await OpportunityPublications
                    .Where(x => ids.Contains(x.OpportunityId))
                    .Select(x => x.OpportunityId)
                    .ToListAsync(ct);
        NormalizeRecords(_ => recorded);
    }

    /// <summary>يعيد المعرفات المحفوظة فقط لتجميع استعلام سجلات النشر.</summary>
    private static long[] SavedIds(
        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Opportunity>> entries
    ) => entries.Select(x => x.Entity.Id).Where(x => x != 0).Distinct().ToArray();

    /// <summary>يختار الانتقالات إلى النشر دون إعادة تسجيل المنشورات السابقة.</summary>
    private List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Opportunity>> NewlyPublished() =>
        ChangeTracker
            .Entries<Opportunity>()
            .Where(x =>
                x.Entity.Status == PublicationStatus.Published
                && (
                    x.State == EntityState.Added
                    || (
                        x.State == EntityState.Modified
                        && x.Property(y => y.Status).OriginalValue != PublicationStatus.Published
                    )
                )
            )
            .ToList();

    // Publication records are looked up in one batched query, never per entity.
    /// <summary>يحمي النسخ الثابتة ويحدث فهارس البحث وطوابير أول نشر والفهرسة.</summary>
    private void NormalizeRecords(Func<long[], List<long>> recordedPublications)
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
        var published = NewlyPublished();
        var savedIds = SavedIds(published);
        var recorded = (savedIds.Length == 0 ? [] : recordedPublications(savedIds)).ToHashSet();
        foreach (var entry in published)
        {
            if (
                entry.Entity.Id == 0
                || (
                    !OpportunityPublications.Local.Any(x => x.OpportunityId == entry.Entity.Id)
                    && !recorded.Contains(entry.Entity.Id)
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

        // Queue public URL changes for IndexNow; the worker submits them in batches.
        foreach (var entry in ChangeTracker.Entries<Opportunity>().Where(IndexNowQueue.Affects).ToList())
            IndexNowQueue.Touch(this, entry.Entity);

        foreach (
            var entry in ChangeTracker
                .Entries<Opportunity>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
        )
        {
            entry.Entity.NormalizedOrganization = TextRules.OrganizationKey(
                entry.Entity.Organization
            );
            entry.Entity.NormalizedReference = DuplicateMatcher.ReferenceKey(
                entry.Entity.ReferenceNumber
            );
            entry.Entity.LocationSearchText = LocationSearch.Index(entry.Entity.Locations);
            entry.Entity.SearchText = LocationSearch.Normalize(
                $"{entry.Entity.Title} {entry.Entity.Organization} {entry.Entity.Summary} {entry.Entity.Category} {entry.Entity.LocationSearchText}"
            );
        }
    }

    /// <summary>يطبق التطبيع وقواعد الثبات قبل الحفظ المتزامن.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeRecords();
        operations.Stage(this);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>يطبق التطبيع وقواعد الثبات ثم يحفظ التغييرات بصورة غير متزامنة.</summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        await NormalizeRecordsAsync(cancellationToken);
        operations.Stage(this);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
