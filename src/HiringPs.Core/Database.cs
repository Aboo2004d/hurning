// تعريف العلاقات والفهارس وتسجيل خدمات النظام في حاوية الاعتماديات.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;
public class HiringDb(DbContextOptions<HiringDb> options) : IdentityDbContext<IdentityUser>(options)
{
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

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ContactMessage>().HasIndex(x => new { x.IsHandled, x.IsRead, x.CreatedAt });
        b.Entity<SourceItem>().HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();
        b.Entity<SourceItem>().HasIndex(x => new { x.Status, x.RetryAt });
        b.Entity<Opportunity>().HasIndex(x => new { x.Status, x.Kind, x.Deadline });
        b.Entity<Opportunity>().HasIndex(x => x.Fingerprint);
        b.Entity<Opportunity>().HasIndex(x => new { x.Kind, x.NormalizedOrganization });
        b.Entity<OpportunitySourceLink>().HasIndex(x => new { x.SourceItemId, x.ChildKey }).IsUnique();
        b.Entity<OpportunitySourceLink>().HasOne(x => x.SourceItem).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserReport>().HasOne(x => x.Opportunity).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<Source>().ToTable(t => t.HasCheckConstraint("CK_Source_Interval", "[IntervalHours] BETWEEN 1 AND 168"));
    }

    private void NormalizeRecords()
    {
        foreach (var entry in ChangeTracker.Entries<Opportunity>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.NormalizedOrganization = TextRules.OrganizationKey(entry.Entity.Organization);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
