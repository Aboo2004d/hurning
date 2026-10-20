using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يتحقق من المصدر ويحفظ إعداداته وجدولته مع حماية التزامن وسياسة URL.</summary>
public sealed class SourceManagementService(HiringDb db, UrlPolicy urlPolicy)
{
    /// <summary>يقرأ حقول المحرر فقط دون إعادة كيان قابل للتتبع إلى الويب.</summary>
    public Task<SourceEditDto?> ReadAsync(int id, CancellationToken ct) =>
        db.Sources.AsNoTracking().Where(x => x.Id == id).Select(x => new SourceEditDto
        {
            Id = x.Id,
            Name = x.Name,
            Url = x.Url,
            Connector = x.Connector,
            Enabled = x.Enabled,
            UsageApproved = x.UsageApproved,
            ApprovalNote = x.ApprovalNote,
            ConfigurationJson = x.ConfigurationJson,
            IntervalHours = x.IntervalHours,
            CollectionStartTime = x.CollectionStartTime,
            Version = x.Version
        }).SingleOrDefaultAsync(ct);

    /// <summary>يفحص شروط المصدر والعنوان قبل تطبيق الإعدادات وإعادة ضبط الجدولة عند تغيرها.</summary>
    public async Task<SourceSaveResultDto> SaveAsync(SourceEditDto input, string actor, CancellationToken ct)
    {
        var errors = new List<string>();
        var previous =
            input.Id == 0
                ? null
                : await db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.Id, ct);
        if (input.Id > 0 && previous is null)
            return new(SourceSaveOutcome.NotFound);
        var duplicateSource = await ExistingSourceAsync(input.Url, input.Id, ct);
        if (duplicateSource is not null)
        {
            return new(SourceSaveOutcome.Duplicate, DuplicateName: duplicateSource.Name);
        }
        if (!Enum.IsDefined(input.Connector))
            errors.Add("Invalid connector.");
        if (input.IntervalHours is < 1 or > 168)
            errors.Add("Interval must be 1–168 hours.");
        if (input.ConfigurationJson.Length > 10000)
            errors.Add("Configuration is too large.");
        try
        {
            var conf =
                JsonSerializer.Deserialize<SourceConfiguration>(input.ConfigurationJson)
                ?? throw new InvalidOperationException("Invalid configuration.");
            if (conf.MaxPages is < 1 or > 50)
                throw new InvalidOperationException("MaxPages must be 1–50.");
            if (input.Enabled && (!input.UsageApproved || input.ApprovalNote.Length == 0))
                throw new InvalidOperationException(
                    "Source usage approval and a record of it are required before enabling."
                );
            if (
                !Uri.TryCreate(input.Url, UriKind.Absolute, out var uri)
                || uri.Scheme != "https"
                || uri.UserInfo.Length > 0
                || !uri.IsDefaultPort
            )
                throw new InvalidOperationException(
                    "Source URL must use public HTTPS without credentials."
                );
            if (
                previous is null
                || previous.Url != input.Url
                || previous.ConfigurationJson != input.ConfigurationJson
                || (!previous.Enabled && input.Enabled)
            )
                await urlPolicy.ValidateAsync(input.Url, conf.AllowedHosts.Append(uri.IdnHost), ct);
            if (
                input.Enabled
                && input.Connector == ConnectorKind.BrowserSelectors
                && conf.ListingLinkSelector.Length == 0
            )
                throw new InvalidOperationException("ListingLinkSelector is required.");
            if (
                input.Connector == ConnectorKind.Automatic
                && (
                    input.Url == OpportunityRss.JobsUrl
                    || input.Url == OpportunityRss.ReliefUrl
                    || input.Url == UnCareersFeed.Url
                )
            )
                throw new InvalidOperationException(
                    "This is an RSS URL; choose its feed connector instead of Automatic HTML discovery."
                );
            if (input.Connector == ConnectorKind.UnrwaInspira && input.Url != UnCareersFeed.Url)
                throw new InvalidOperationException(
                    "UNRWA/Inspira connector requires https://careers.un.org/jobfeed."
                );
            if (input.Enabled && input.Connector == ConnectorKind.ReliefWeb)
                throw new InvalidOperationException(
                    "ReliefWeb adapter is pending appname approval and verification."
                );
            if (
                input.Connector is ConnectorKind.JobsRss or ConnectorKind.ReliefWebRss
                && input.Url != OpportunityRss.Endpoint(input.Connector)
            )
                throw new InvalidOperationException(
                    "RSS connector requires its official tested feed URL."
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add(
                ex is InvalidOperationException
                    ? ex.Message
                    : "Invalid public source URL/configuration."
            );
        }

        if (errors.Count > 0) return new(SourceSaveOutcome.Invalid, errors);
        var entity = input.Id == 0 ? new Source() : await db.Sources.FindAsync([input.Id], ct);
        if (entity is null)
            return new(SourceSaveOutcome.NotFound);
        if (input.Id != 0)
            db.Entry(entity).Property(x => x.Version).OriginalValue = input.Version;
        var scheduleChanged =
            entity.IntervalHours != input.IntervalHours
            || entity.CollectionStartTime != input.CollectionStartTime
            || (!entity.Enabled && input.Enabled)
            || input.Id == 0;
        var reconfigured =
            entity.Url != input.Url
            || entity.ConfigurationJson != input.ConfigurationJson
            || entity.Connector != input.Connector;
        entity.Name = input.Name;
        entity.Url = input.Url;
        entity.Connector = input.Connector;
        entity.Enabled = input.Enabled;
        entity.UsageApproved = input.UsageApproved;
        entity.ApprovalNote = input.ApprovalNote;
        entity.ConfigurationJson = input.ConfigurationJson;
        entity.IntervalHours = input.IntervalHours;
        entity.CollectionStartTime = input.CollectionStartTime;
        if (scheduleChanged || reconfigured)
        {
            entity.ScheduleAnchorDate = SourceSchedule.LocalDate(DateTimeOffset.UtcNow);
            entity.NextDueAt = SourceSchedule.NextAfter(entity, DateTimeOffset.UtcNow);
            entity.CollectionRetryAt = null;
        }

        if (input.Id == 0)
            db.Sources.Add(entity);
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "Source.Edit", Detail = $"Source {input.Id}; enabled {input.Enabled}" });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(SourceSaveOutcome.Conflict, ["Source changed; reload before saving."]);
        }

        return new(SourceSaveOutcome.Saved);
    }

    /// <summary>يبحث عن مصدر له العنوان نفسه مع استثناء سجل التعديل الحالي.</summary>
    public async Task<SourceDuplicateSummaryDto?> FindDuplicateAsync(string url, int excludedId, CancellationToken ct)
    {
        var source = await ExistingSourceAsync(url, excludedId, ct);
        return source is null ? null : new(source.Id, source.Name);
    }

    /// <summary>يقارن عناوين المصادر بصيغتها المتفق عليها دون اعتبار fragment.</summary>
    private async Task<Source?> ExistingSourceAsync(
        string url,
        int excludedId,
        CancellationToken ct
    )
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var requested))
            return null;
        var sources = await db
            .Sources.AsNoTracking()
            .Where(s => s.Id != excludedId)
            .ToListAsync(ct);
        return sources.FirstOrDefault(s =>
            Uri.TryCreate(s.Url, UriKind.Absolute, out var saved)
            && Uri.Compare(
                requested,
                saved,
                UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
                UriFormat.UriEscaped,
                StringComparison.Ordinal
            ) == 0
        );
    }
}
