using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحفظ التعديل الإداري للفرصة ويحميه من الاستخراج اللاحق مع تدقيق المصدر والموقع.</summary>
public sealed class OpportunityManagementService(HiringDb db)
{
    /// <summary>يقرأ حقول المحرر فقط دون إعادة كيان قابل للتتبع إلى الويب.</summary>
    public Task<OpportunityEditDto?> ReadAsync(long id, CancellationToken ct) =>
        db.Opportunities.AsNoTracking().Where(x => x.Id == id).Select(x => new OpportunityEditDto
        {
            Id = x.Id,
            Kind = x.Kind,
            Title = x.Title,
            Organization = x.Organization,
            OriginalLanguage = x.OriginalLanguage,
            Summary = x.Summary,
            Locations = x.Locations,
            Category = x.Category,
            EmploymentType = x.EmploymentType,
            ExperienceLevel = x.ExperienceLevel,
            WorkMode = x.WorkMode,
            ReferenceNumber = x.ReferenceNumber,
            Duration = x.Duration,
            Eligibility = x.Eligibility,
            Cost = x.Cost,
            ApplyUrl = x.ApplyUrl,
            Deadline = x.Deadline,
            Status = x.Status,
            ClosureEvidence = x.ClosureEvidence,
            Version = x.Version,
            ReviewReason = x.ReviewReason
        }).SingleOrDefaultAsync(ct);

    /// <summary>يفحص حقول الفرصة والإغلاق ويحفظ الروابط والاعتماد الإداري دون MVC.</summary>
    public async Task<OpportunitySaveResultDto> SaveAsync(OpportunityEditDto input, string? sourceUrl, string actor, CancellationToken ct)
    {
        var errors = new List<string>();
        foreach (var error in OpportunityService.Validate(ToCandidate(input), allowUnrecognizedLocation: true))
            errors.Add(error);
        if (!Enum.IsDefined(input.Status))
            errors.Add("Invalid publication status.");
        if (
            input.Status == PublicationStatus.Archived
            && string.IsNullOrWhiteSpace(input.ClosureEvidence)
            && input.Deadline is null
        )
            errors.Add("Closure evidence is required without a deadline.");
        if (sourceUrl?.Length > 1500)
            errors.Add("Source URL is too long.");
        if (
            !string.IsNullOrWhiteSpace(sourceUrl)
            && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        )
            errors.Add("Source URL must use HTTPS.");
        if (
            input.Id == 0
            && string.IsNullOrWhiteSpace(sourceUrl)
            && string.IsNullOrWhiteSpace(input.ApplyUrl)
        )
            errors.Add("A source or application link is required.");
        if (errors.Count > 0) return new(OpportunitySaveOutcome.Invalid, errors);
        var entity =
            input.Id == 0 ? new Opportunity() : await db.Opportunities.FindAsync([input.Id], ct);
        if (entity is null)
            return new(OpportunitySaveOutcome.NotFound);
        if (input.Id != 0)
            db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        entity.Kind = input.Kind;
        entity.Title = input.Title.Trim();
        entity.Organization = input.Organization.Trim();
        entity.OriginalLanguage = input.OriginalLanguage;
        entity.Summary = input.Summary.Trim();
        entity.Locations = input.Locations;
        entity.Category = input.Category;
        entity.EmploymentType = input.EmploymentType;
        entity.ExperienceLevel = input.ExperienceLevel;
        entity.WorkMode = input.WorkMode;
        entity.ReferenceNumber = input.ReferenceNumber;
        entity.Duration = input.Duration;
        entity.Eligibility = input.Eligibility;
        entity.Cost = input.Cost;
        entity.ApplyUrl = input.ApplyUrl;
        entity.Deadline = input.Deadline;
        entity.Status = input.Status;
        entity.ClosureEvidence = input.ClosureEvidence;
        entity.AdminOverride = true;
        entity.LastVerifiedAt = DateTimeOffset.UtcNow;
        entity.ReviewReason = "";
        if (
            entity.Status == PublicationStatus.Published
            && entity.Deadline is { } deadline
            && deadline < TextRules.PalestineToday()
        )
        {
            entity.Status = PublicationStatus.Archived;
            entity.ClosureEvidence = "Published deadline has passed.";
        }

        entity.Fingerprint = TextRules.Hash(
            TextRules.Normalize(
                $"{entity.Kind}|{entity.Title}|{entity.Organization}|{entity.Locations}|{entity.ReferenceNumber}|{entity.Deadline}"
            )
        );
        if (input.Id == 0)
        {
            db.Opportunities.Add(entity);
            var source = await db.Sources.FirstOrDefaultAsync(x => x.Name == "Manual entries", ct);
            if (source is null)
            {
                source = new Source
                {
                    Name = "Manual entries",
                    Url = "https://hiring.ps",
                    Connector = ConnectorKind.BrowserSelectors,
                    Enabled = false,
                };
                db.Sources.Add(source);
            }

            var item = new SourceItem
            {
                Source = source,
                ExternalId = Guid.NewGuid().ToString("N"),
                Url = sourceUrl ?? input.ApplyUrl,
                Content = input.Summary,
                Status = ProcessingStatus.Published,
                ContentHash = TextRules.Hash(input.Summary),
            };
            db.SourceItems.Add(item);
            db.SourceLinks.Add(
                new()
                {
                    Opportunity = entity,
                    SourceItem = item,
                    ChildKey = "manual",
                }
            );
        }

        Audit(actor, "Opportunity.Edit", $"Opportunity {input.Id}; status {entity.Status}");
        if (entity.Status is PublicationStatus.Published or PublicationStatus.Archived)
        {
            var known = await LocationCatalog.ApprovedAsync(db, ct);
            foreach (
                var location in LocationCatalog
                    .Split(entity.Locations)
                    .Where(x => !known.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            )
                Audit(actor, "Location.Approved", location);
        }

        if (input.Id > 0)
        {
            var rawIds = await db
                .SourceLinks.Where(x => x.OpportunityId == input.Id)
                .Select(x => x.SourceItemId)
                .Distinct()
                .ToListAsync(ct);
            foreach (var rawId in rawIds)
            {
                var raw = await db.SourceItems.FindAsync([rawId], ct);
                if (raw is null || raw.Status != ProcessingStatus.NeedsReview)
                    continue;
                var links = await db
                    .SourceLinks.Include(x => x.Opportunity)
                    .Where(x => x.SourceItemId == rawId)
                    .ToListAsync(ct);
                var pending = links
                    .Where(x => x.Opportunity.Status == PublicationStatus.NeedsReview)
                    .ToList();
                raw.Status =
                    pending.Count == 0 ? ProcessingStatus.Published : ProcessingStatus.NeedsReview;
                raw.Error = string.Join(
                        " ",
                        pending.Select(x => x.Opportunity.ReviewReason).Distinct()
                    )
                    .Truncate(1800);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(OpportunitySaveOutcome.Conflict, ["The record changed; reload before saving."]);
        }

        return new(OpportunitySaveOutcome.Saved);
    }

    /// <summary>يبني مرشح تحقق داخليًا؛ لا يربطه بـ DbContext.</summary>
    private static Opportunity ToCandidate(OpportunityEditDto input) => new() { Id = input.Id, Kind = input.Kind, Title = input.Title, Organization = input.Organization, OriginalLanguage = input.OriginalLanguage, Summary = input.Summary, Locations = input.Locations, Category = input.Category, EmploymentType = input.EmploymentType, ExperienceLevel = input.ExperienceLevel, WorkMode = input.WorkMode, ReferenceNumber = input.ReferenceNumber, Duration = input.Duration, Eligibility = input.Eligibility, Cost = input.Cost, ApplyUrl = input.ApplyUrl, Deadline = input.Deadline, Status = input.Status, ClosureEvidence = input.ClosureEvidence, Version = input.Version, ReviewReason = input.ReviewReason };

    /// <summary>يضيف التدقيق إلى نفس حفظ التعديل.</summary>
    private void Audit(string actor, string action, string detail) =>
        db.Audit.Add(new AuditEntry { Actor = actor, Action = action, Detail = detail });
}
