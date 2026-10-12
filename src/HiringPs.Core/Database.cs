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
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<SourceItem>().HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();
        b.Entity<SourceItem>().HasIndex(x => new { x.Status, x.RetryAt });
        b.Entity<Opportunity>().HasIndex(x => new { x.Status, x.Kind, x.Deadline });
        b.Entity<Opportunity>().HasIndex(x => x.Fingerprint);
        b.Entity<Opportunity>().HasIndex(x=>new{x.Kind,x.NormalizedOrganization});
        b.Entity<OpportunitySourceLink>().HasIndex(x => new { x.SourceItemId, x.ChildKey }).IsUnique();
        b.Entity<OpportunitySourceLink>().HasOne(x => x.SourceItem).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserReport>().HasOne(x => x.Opportunity).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.Entity<Source>().ToTable(t => t.HasCheckConstraint("CK_Source_Interval", "[IntervalHours] BETWEEN 1 AND 168"));
    }
    private void NormalizeRecords()
    {
        foreach(var entry in ChangeTracker.Entries<Opportunity>().Where(x=>x.State is EntityState.Added or EntityState.Modified))entry.Entity.NormalizedOrganization=TextRules.Normalize(entry.Entity.Organization);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess){NormalizeRecords();return base.SaveChanges(acceptAllChangesOnSuccess);}
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,CancellationToken cancellationToken=default){NormalizeRecords();return base.SaveChangesAsync(acceptAllChangesOnSuccess,cancellationToken);}
}
public static class Registration
{
    public static IServiceCollection AddHiring(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Hiring") ?? throw new InvalidOperationException("Configure ConnectionStrings:Hiring through User Secrets or environment variables.");
        services.AddDbContext<HiringDb>(o => o.UseSqlServer(connection));
        services.AddScoped<OpportunityService>(); services.AddScoped<CollectionPipeline>();
        services.AddSingleton<UrlPolicy>(); services.AddScoped<BrowserCollector>();
        services.AddScoped<AttachmentReader>();
        services.AddHttpClient<AiExtractor>(c => c.Timeout = TimeSpan.FromSeconds(90));
        return services;
    }
}
