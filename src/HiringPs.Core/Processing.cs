// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public class OpportunityService(HiringDb db)
{
    // Validation and duplicate rules live in ExtractionValidator/DuplicateMatcher; these keep callers stable.
    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent) =>
        ExtractionValidator.VerifiedOptionalApplyUrl(value, sourceContent);

    public static bool IsWhatsAppHost(string host) => ExtractionValidator.IsWhatsAppHost(host);

    public static string ResolveApplyUrl(string? aiUrl, SourceItem item) =>
        ExtractionValidator.ResolveApplyUrl(aiUrl, item);

    public static List<string> Validate(
        Opportunity o,
        string? sourceContent = null,
        bool allowUnrecognizedLocation = false,
        IEnumerable<string>? approvedLocations = null
    ) =>
        ExtractionValidator.Validate(o, sourceContent, allowUnrecognizedLocation, approvedLocations);

    public static bool DateEvidenceMatches(string evidence, DateOnly claimed) =>
        ExtractionValidator.DateEvidenceMatches(evidence, claimed);

    public static bool PotentialDuplicate(Opportunity incoming, Opportunity existing) =>
        DuplicateMatcher.PotentialDuplicate(incoming, existing);

    public async Task ApplyExtractionAsync(
        SourceItem item,
        ExtractionResult result,
        CancellationToken ct
    )
    {
        ExtractionValidator.EnsureShape(result);
        if (!result.InScope)
        {
            item.Status = ProcessingStatus.NeedsReview;
            item.Error = "AI marked out of scope; verify before excluding.";
            item.ExtractionPromptVersionId = result.PromptVersionId;
            await db.SaveChangesAsync(ct);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var allReviewed = true;
        var reviewReasons = new List<string>();
        var approvedLocations = await LocationCatalog.ApprovedAsync(db, ct);
        var sourceContent = SourceEvidence.ForItem(item);
        var activeRules = result.PromptVersionId is { } versionId
            ? await db.PromptVersions.AsNoTracking().SingleAsync(x => x.Id == versionId, ct)
            : null;
        var aliases = await DuplicateMatcher.AliasesAsync(db, ct);
        // One query for this item's links and one for their known dates, not one per child/date.
        var links = await db
            .SourceLinks.Include(x => x.Opportunity)
            .Where(x => x.SourceItemId == item.Id)
            .ToListAsync(ct);
        var linkByKey = links.ToDictionary(x => x.ChildKey, StringComparer.OrdinalIgnoreCase);
        var ownIds = links.Select(x => x.OpportunityId).Distinct().ToArray();
        var knownDates = (
            await db
                .OpportunityDates.AsNoTracking()
                .Where(x => ownIds.Contains(x.OpportunityId))
                .Select(x => new
                {
                    x.OpportunityId,
                    x.Kind,
                    x.EvidenceHash,
                })
                .ToListAsync(ct)
        )
            .Select(x => (x.OpportunityId, x.Kind, x.EvidenceHash))
            .ToHashSet();
        var nearMatches = new List<(Opportunity, long, double, string)>();
        var now = DateTimeOffset.UtcNow;
        foreach (var e in result.Opportunities)
        {
            linkByKey.TryGetValue(e.ChildKey, out var link);
            if (
                link is not null
                && PromptWorkflow.Protected(link.Opportunity, result.PromptVersionId)
            )
                continue;
            var candidate = new Opportunity
            {
                Kind = e.Kind,
                Title = e.Title,
                Organization = e.Organization,
                OriginalLanguage = e.OriginalLanguage,
                Summary = e.Summary,
                Locations = e.Locations,
                Category = e.Category,
                EmploymentType = e.EmploymentType,
                ExperienceLevel = e.ExperienceLevel,
                WorkMode = e.WorkMode,
                ReferenceNumber = e.ReferenceNumber,
                Duration = e.Duration,
                Eligibility = e.Eligibility,
                Cost = e.Cost,
                ApplyUrl = e.ApplyUrl,
                Deadline = e.Deadline,
                AttachmentsJson = item.AttachmentsJson,
            };
            candidate.ApplyUrl = ExtractionValidator.ResolveApplyUrl(e.ApplyUrl, item);
            if (
                item.Source.Connector == ConnectorKind.PalCareer
                && result.Opportunities.Count == 1
                && !TextEvidence.TitleContains(sourceContent, candidate.Title)
            )
            {
                var originalTitle = SourceEvidence.PrimaryPalCareerTitle(sourceContent);
                if (originalTitle.Length > 0)
                    candidate.Title = originalTitle;
            }

            var checkedDates = DateEvidenceRules.Check(e, sourceContent);
            candidate.Deadline = checkedDates
                .Dates.FirstOrDefault(x => x.Kind == OpportunityDateKind.Deadline)
                ?.Date;
            if (
                link?.Opportunity.Deadline is { } previousDeadline
                && candidate.Deadline is { } nextDeadline
                && nextDeadline != previousDeadline
                && (!e.DeadlineIsExtension || nextDeadline <= previousDeadline)
            )
            {
                checkedDates = new(
                    checkedDates.Dates.Where(x => x.Kind != OpportunityDateKind.Deadline).ToList(),
                    checkedDates
                        .Errors.Append(
                            "Date validation: changed closing date requires explicit verified extension evidence."
                        )
                        .ToList()
                );
                candidate.Deadline = null;
            }
            var errors = ExtractionValidator.Validate(
                candidate,
                sourceContent,
                allowUnrecognizedLocation: e.IsInPalestine == true,
                approvedLocations: approvedLocations
            );
            if (
                e.IsInPalestine == false
                && !errors.Contains("Local geographic scope needs review.")
            )
                errors.Add("Local geographic scope needs review.");
            errors.AddRange(checkedDates.Errors);
            if (sourceContent.Contains(PdfTextFiles.ReviewMarker, StringComparison.Ordinal))
                errors.Add(
                    "PDF OCR transcription requires visual verification of the original page before publication."
                );
            if (activeRules is not null)
            {
                if (!activeRules.AllowedKinds.Split(',').Contains(candidate.Kind.ToString()))
                    errors.Add("Opportunity kind is disabled by the active prompt conditions.");
                if (!activeRules.AllowUnknownDeadline && candidate.Deadline is null)
                    errors.Add(
                        "Deadline is unspecified; verify original source before publication."
                    );
            }
            var relatedRole = SourceEvidence.IsRelatedOnly(item, candidate.Title);
            if (relatedRole)
            {
                errors.Remove("Original title not found in source text.");
                errors.Add(
                    "Extracted role belongs to related listings, not the original announcement."
                );
            }

            if (candidate.ApplyUrl.Length == 0)
                errors.Add("No valid application or original announcement URL available.");
            if (
                !relatedRole
                && candidate.Organization.Length > 0
                && !TextEvidence.OrganizationContains(sourceContent, candidate.Organization)
            )
                errors.Add("Organization evidence missing.");
            if (
                candidate.ReferenceNumber.Length > 0
                && !TextEvidence.Contains(sourceContent, candidate.ReferenceNumber)
            )
                errors.Add("Reference number evidence missing.");
            if (!TextEvidence.Contains(sourceContent, e.LocationEvidence))
                errors.Add("Location evidence missing.");
            if (
                candidate.Deadline is not null
                && !TextEvidence.Contains(sourceContent, e.DeadlineEvidence)
            )
                errors.Add("Deadline evidence missing.");
            if (
                candidate.Deadline is { } claimed
                && !ExtractionValidator.DateEvidenceMatches(e.DeadlineEvidence, claimed)
            )
                errors.Add("Deadline quotation does not independently confirm the date.");
            var relevantReason = SourceEvidence.RelevantReviewReason(e.ReviewReason);
            if (relevantReason.Length > 0)
                errors.Add(relevantReason);
            candidate.Fingerprint = TextRules.Hash(
                TextRules.Normalize(
                    $"{candidate.Kind}|{candidate.Title}|{candidate.Organization}|{candidate.Locations}|{candidate.ReferenceNumber}|{candidate.Deadline}"
                )
            );
            var organizationKeys = aliases.Keys(TextRules.OrganizationKey(candidate.Organization));
            // Records of this same announcement (other lots/roles) are never its duplicates.
            var excluded = ownIds.Append(link?.OpportunityId ?? 0).Distinct().ToArray();
            var reference = await DuplicateMatcher.ExactReferenceAsync(
                db,
                candidate,
                organizationKeys,
                excluded,
                ct
            );
            var exact =
                reference.Exact
                ?? (
                    reference.Ambiguous.Count > 0
                        ? null
                        : await db.Opportunities.FirstOrDefaultAsync(
                            x =>
                                x.Fingerprint == candidate.Fingerprint
                                && x.Status != PublicationStatus.Hidden
                                && !excluded.Contains(x.Id),
                            ct
                        )
                );
            // Only deterministic identifiers justify automatic cross-source merging.
            if (
                exact is not null
                && candidate.ReferenceNumber.Length > 0
                && candidate.Organization.Length > 0
                && errors.Count == 0
            )
            {
                if (link is null)
                    db.SourceLinks.Add(
                        new()
                        {
                            SourceItemId = item.Id,
                            ChildKey = e.ChildKey,
                            OpportunityId = exact.Id,
                        }
                    );
                else
                    link.OpportunityId = exact.Id;
                if (!PromptWorkflow.Protected(exact, result.PromptVersionId))
                    exact.LastVerifiedAt = now;
                continue;
            }

            var peers = new List<(long Id, double Score, string Reason)>();
            if (exact is not null)
            {
                errors.Add(
                    "Possible duplicate without a unique reference; administrator review required."
                );
                peers.Add((exact.Id, 1, "Same fingerprint or reference; validation needs review."));
            }
            foreach (var ambiguous in reference.Ambiguous)
                peers.Add((ambiguous, 1, "Same organisation and reference number on several records."));
            if (reference.Ambiguous.Count > 0)
                errors.Add(
                    "Reference number matches several records; compare and merge in administration."
                );
            if (exact is null && link is null)
            {
                var similar = await db
                    .Opportunities.AsNoTracking()
                    .Where(x =>
                        x.Kind == candidate.Kind
                        && organizationKeys.Contains(x.NormalizedOrganization)
                        && !excluded.Contains(x.Id)
                        && (
                            x.Status == PublicationStatus.Published
                            || x.Status == PublicationStatus.NeedsReview
                            || x.Status == PublicationStatus.Archived
                        )
                    )
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .ToListAsync(ct);
                if (similar.Any(x => DuplicateMatcher.PotentialDuplicate(candidate, x, sameOrganization: true)))
                    errors.Add(
                        "Possible repost or similar role across sources; compare and merge in administration if identical."
                    );
                foreach (var peer in similar)
                    if (DuplicateMatcher.NearMatch(candidate, peer) is { } match)
                        peers.Add((peer.Id, match.Score, match.Reason));
            }

            var target = link?.Opportunity;
            if (target is null)
            {
                target = candidate;
                db.Opportunities.Add(target);
                db.SourceLinks.Add(
                    new()
                    {
                        Opportunity = target,
                        SourceItemId = item.Id,
                        ChildKey = e.ChildKey,
                    }
                );
            }
            else if (target.IsMergeTarget)
            {
                // A merged record keeps its reviewed text; a verified deadline extension still applies.
                if (candidate.Deadline is not null && errors.Count == 0)
                    target.Deadline = candidate.Deadline;
            }
            else
            {
                target.Kind = candidate.Kind;
                target.Title = candidate.Title;
                target.Organization = candidate.Organization;
                target.OriginalLanguage = candidate.OriginalLanguage;
                target.Summary = candidate.Summary;
                target.Locations = candidate.Locations;
                target.Category = candidate.Category;
                target.EmploymentType = candidate.EmploymentType;
                target.ExperienceLevel = candidate.ExperienceLevel;
                target.WorkMode = candidate.WorkMode;
                target.ReferenceNumber = candidate.ReferenceNumber;
                target.Duration = candidate.Duration;
                target.Eligibility = candidate.Eligibility;
                target.Cost = candidate.Cost;
                target.ApplyUrl = candidate.ApplyUrl;
                if (candidate.Deadline is not null)
                    target.Deadline = candidate.Deadline;
                target.Fingerprint = candidate.Fingerprint;
                target.AttachmentsJson = candidate.AttachmentsJson;
            }
            foreach (var peer in peers)
                nearMatches.Add((target, peer.Id, peer.Score, peer.Reason));

            target.PromptVersionId = result.PromptVersionId;
            foreach (var date in checkedDates.Dates)
            {
                var evidenceHash = TextRules.Hash(
                    $"{date.Kind}|{date.Date:yyyy-MM-dd}|{date.Label}|{date.Evidence}"
                );
                if (target.Id != 0 && !knownDates.Add((target.Id, date.Kind, evidenceHash)))
                    continue;
                db.OpportunityDates.Add(
                    new()
                    {
                        Opportunity = target,
                        SourceItemId = item.Id,
                        PromptVersionId = result.PromptVersionId,
                        Kind = date.Kind,
                        Date = date.Date,
                        Evidence = date.Evidence,
                        Label = date.Label.Truncate(300),
                        EvidenceHash = evidenceHash,
                        IsExtension =
                            date.Kind == OpportunityDateKind.Deadline && e.DeadlineIsExtension,
                    }
                );
                if (date.Kind == OpportunityDateKind.Publication)
                    target.SourcePublishedAt = DateEvidenceRules.AtPalestineMidnight(date.Date);
                if (date.Kind == OpportunityDateKind.Modification)
                    target.SourceModifiedAt = DateEvidenceRules.AtPalestineMidnight(date.Date);
            }
            target.LastVerifiedAt = now;
            var expired = target.Deadline is { } deadline && deadline < TextRules.PalestineToday();
            if (target.IsMergeTarget)
            {
                // Problems of one merged source are reported on its item, not on the merged record.
                if (expired && target.Status == PublicationStatus.Published)
                {
                    target.Status = PublicationStatus.Archived;
                    target.ClosureEvidence = "Published source deadline has passed.";
                }
            }
            else
            {
                target.ReviewReason = string.Join(" ", errors);
                target.Status =
                    errors.Count > 0 ? PublicationStatus.NeedsReview
                    : expired ? PublicationStatus.Archived
                    : PublicationStatus.Published;
                if (target.Status == PublicationStatus.Archived)
                    target.ClosureEvidence = "Published source deadline has passed.";
            }
            if (errors.Count > 0)
                allReviewed = false;
            reviewReasons.AddRange(errors);
        }

        var currentKeys = result
            .Opportunities.Select(x => x.ChildKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = links.Where(x => !currentKeys.Contains(x.ChildKey)).ToList();
        foreach (
            var link in removed.Where(x =>
                !PromptWorkflow.Protected(x.Opportunity, result.PromptVersionId)
            )
        )
        {
            link.Opportunity.Status = PublicationStatus.NeedsReview;
            link.Opportunity.ReviewReason =
                "Role/lot disappeared from revised source; verify before deciding closure.";
            allReviewed = false;
        }

        item.ExtractionPromptVersionId = result.PromptVersionId;
        item.Status = allReviewed ? ProcessingStatus.Published : ProcessingStatus.NeedsReview;
        item.Error = allReviewed
            ? ""
            : string.Join(
                    " ",
                    reviewReasons
                        .Concat(
                            removed
                                .Where(x => x.Opportunity.Status == PublicationStatus.NeedsReview)
                                .Select(x => x.Opportunity.ReviewReason)
                        )
                        .Distinct()
                )
                .Truncate(1800);
        item.ExtractionJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
        await db.SaveChangesAsync(ct);
        if (nearMatches.Count > 0)
        {
            await DuplicateMatcher.RecordAsync(db, nearMatches, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task ArchiveExpiredAsync(CancellationToken ct, int? sourceId = null)
    {
        var today = TextRules.PalestineToday();
        var query = db.Opportunities.Where(x =>
            x.Status == PublicationStatus.Published && x.Deadline != null && x.Deadline < today
        );
        if (sourceId is not null)
            query = query.Where(x => x.SourceLinks.Any(l => l.SourceItem.SourceId == sourceId));
        await query.ExecuteUpdateAsync(
            s =>
                s.SetProperty(x => x.Status, PublicationStatus.Archived)
                    .SetProperty(x => x.ClosureEvidence, "Published deadline has passed."),
            ct
        );
    }
}

public static class Strings
{
    public static string Truncate(this string text, int max) =>
        text.Length <= max ? text : text[..max];
}

public class CollectionPipeline(
    HiringDb db,
    BrowserCollector browser,
    OpportunityService opportunities,
    IConfiguration config,
    ILogger<CollectionPipeline> log,
    IServiceScopeFactory scopes
)
{
    private static int zoneWarningLogged;

    public async Task TickAsync(CancellationToken ct)
    {
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct))
            return;
        _ = TextRules.PalestineTimeZone; // resolve once so a fallback warning is known
        if (
            TextRules.PalestineTimeZoneWarning is { } zoneWarning
            && Interlocked.Exchange(ref zoneWarningLogged, 1) == 0
        )
            log.LogWarning("{Warning}", zoneWarning);
        var heartbeat = await db.Heartbeats.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (heartbeat is null)
        {
            heartbeat = new();
            db.Heartbeats.Add(heartbeat);
        }

        heartbeat.At = DateTimeOffset.UtcNow;
        heartbeat.State = "Running";
        await db.SaveChangesAsync(ct);
        var finalState = "Failed";
        try
        {
            await RunAsync(gate, ct);
            finalState = "Idle";
        }
        finally
        {
            // A failed save must never leave the heartbeat claiming "Running".
            await FinishHeartbeatAsync(heartbeat.Id, finalState);
        }
    }

    private async Task FinishHeartbeatAsync(int id, string state)
    {
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var scope = scopes.CreateAsyncScope();
            var heartbeatDb = scope.ServiceProvider.GetRequiredService<HiringDb>();
            await heartbeatDb
                .Heartbeats.Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.At, DateTimeOffset.UtcNow)
                            .SetProperty(x => x.State, state),
                    budget.Token
                );
        }
        catch (Exception ex)
        {
            log.LogWarning("Heartbeat update failed ({Type}).", ex.GetType().Name);
        }
    }

    private async Task RunAsync(SqlRunLock gate, CancellationToken ct)
    {
        var activePrompt = await PromptWorkflow.ActivateAsync(db, ct);
        await RetentionCleanup.RunAsync(db, config, ct);
        var now = DateTimeOffset.UtcNow;
        var requests = db.Set<SourceCollectionRequest>();
        await requests
            .Where(x =>
                (x.Status == "Pending" || x.Status == "Running")
                && (!x.Source.Enabled || !x.Source.UsageApproved)
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Cancelled")
                        .SetProperty(x => x.Error, "Source disabled or usage approval removed.")
                        .SetProperty(x => x.FinishedAt, (DateTimeOffset?)now),
                ct
            );
        var manual = await requests
            .Include(x => x.Source)
            .Where(x => x.Status == "Pending" || x.Status == "Running")
            .OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);
        if (manual is not null)
        {
            manual.Status = "Running";
            manual.StartedAt ??= now;
            await db.SaveChangesAsync(ct);
        }

        var selectedSources = manual is not null
            ? (manual.DiscoveryFinished ? new List<Source>() : new List<Source> { manual.Source })
            : await db
                .Sources.Where(x =>
                    x.Enabled
                    && x.UsageApproved
                    && (
                        x.CollectionRetryAt != null
                            ? x.CollectionRetryAt <= now
                            : x.NextDueAt <= now
                    )
                )
                .OrderBy(x => x.NextDueAt)
                .Take(3)
                .ToListAsync(ct);
        foreach (var source in selectedSources)
            await DiscoverSourceAsync(gate, source, manual, activePrompt, ct);

        await ProcessPendingAsync(gate, manual, activePrompt, ct);

        if (manual is not null && manual.Status != "Failed")
        {
            var remaining = await db.SourceItems.AnyAsync(
                x =>
                    x.SourceId == manual.SourceId
                    && (
                        x.Status == ProcessingStatus.Discovered
                        || (
                            x.Status == ProcessingStatus.Retry
                            && (x.RetryAt == null || x.RetryAt <= DateTimeOffset.UtcNow)
                        )
                    ),
                ct
            );
            if (!remaining)
            {
                manual.Status = "Completed";
                manual.FinishedAt = DateTimeOffset.UtcNow;
                var counts = await db
                    .SourceItems.Where(x => x.SourceId == manual.SourceId)
                    .GroupBy(x => x.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);
                manual.Error =
                    "Discovery round finished; source item totals: "
                    + string.Join(", ", counts.Select(x => $"{x.Status}: {x.Count}"))
                    + ". Pending AI/retries are not published successes.";
            }
        }

        await opportunities.ArchiveExpiredAsync(ct, manual?.SourceId);
        await db.SaveChangesAsync(ct);
    }

    private async Task DiscoverSourceAsync(
        SqlRunLock gate,
        Source source,
        SourceCollectionRequest? manual,
        PromptVersion activePrompt,
        CancellationToken ct
    )
    {
        await gate.VerifyAsync(ct);
        var run = new ScrapeRun { SourceId = source.Id };
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        var succeeded = false;
        var reverifyBefore = DateTimeOffset.UtcNow - SourceHealth.ReverifyInterval(config);
        try
        {
            var configurationHash = TextRules.Hash(
                $"{source.Connector}|{source.Url}|{source.ConfigurationJson}"
            );
            var checkpoint = await db.DiscoveryCheckpoints.SingleOrDefaultAsync(
                x => x.SourceId == source.Id,
                ct
            );
            if (checkpoint is null)
            {
                checkpoint = new()
                {
                    SourceId = source.Id,
                    ConfigurationHash = configurationHash,
                    NextUrl = source.Url,
                };
                db.DiscoveryCheckpoints.Add(checkpoint);
                await db.SaveChangesAsync(ct);
            }
            else if (checkpoint.ConfigurationHash != configurationHash)
            {
                // Configuration changes explicitly start a fresh cursor; saved items remain intact.
                checkpoint.ConfigurationHash = configurationHash;
                checkpoint.NextUrl = source.Url;
                checkpoint.CompletedPages = checkpoint.ReplayClicks = checkpoint.Found = 0;
                checkpoint.Completed = false;
                checkpoint.VisitedUrlsJson = "[]";
                await db.SaveChangesAsync(ct);
            }
            run.Found = checkpoint.Found;
            await browser.DiscoverResumableAsync(
                source,
                checkpoint,
                async (batch, token) =>
                {
                    await gate.VerifyAsync(token);
                    // Keep failed batch tracking isolated: a later error save must never commit a partial page.
                    await using var pageScope = scopes.CreateAsyncScope();
                    var pageDb = pageScope.ServiceProvider.GetRequiredService<HiringDb>();
                    var savedCheckpoint = await pageDb.DiscoveryCheckpoints.SingleAsync(
                        x => x.SourceId == source.Id,
                        token
                    );
                    var savedRun = await pageDb.Runs.SingleAsync(x => x.Id == run.Id, token);
                    await using var pageTransaction =
                        await pageDb.Database.BeginTransactionAsync(token);
                    await SaveDiscoveredAsync(
                        pageDb,
                        source.Id,
                        batch,
                        savedRun,
                        activePrompt.Id,
                        reverifyBefore,
                        token
                    );
                    var visited =
                        JsonSerializer.Deserialize<List<string>>(
                            savedCheckpoint.VisitedUrlsJson
                        ) ?? [];
                    if (
                        source.Connector == ConnectorKind.Automatic
                        && savedCheckpoint.NextUrl.Length > 0
                    )
                        visited.Add(savedCheckpoint.NextUrl);
                    savedCheckpoint.VisitedUrlsJson = JsonSerializer.Serialize(
                        visited.Distinct()
                    );
                    savedCheckpoint.CompletedPages = batch.CompletedPages;
                    savedCheckpoint.NextUrl = batch.NextUrl ?? "";
                    savedCheckpoint.ReplayClicks = batch.ReplayClicks;
                    savedCheckpoint.Completed = batch.NextUrl is null;
                    savedCheckpoint.Found += batch.Items.Count;
                    savedCheckpoint.UpdatedAt = DateTimeOffset.UtcNow;
                    savedRun.Found = savedCheckpoint.Found;
                    await pageDb.SaveChangesAsync(token);
                    await pageTransaction.CommitAsync(token);
                    await db.Entry(checkpoint).ReloadAsync(token);
                    await db.Entry(run).ReloadAsync(token);
                },
                ct
            );
            db.DiscoveryCheckpoints.Remove(checkpoint);
            await db.Entry(source).ReloadAsync(ct);
            var finished = DateTimeOffset.UtcNow;
            source.LastSuccessAt = finished;
            source.LastError = "";
            source.CollectionRetryAt = null;
            await SourceHealth.RecordSuccessAsync(db, source, run, config, ct);
            source.ScheduleAnchorDate ??= SourceSchedule.LocalDate(finished);
            if (manual is null || source.NextDueAt <= finished)
                source.NextDueAt = SourceSchedule.NextAfter(source, finished);
            if (manual is not null)
                manual.DiscoveryFinished = true;
            run.Status = "Success";
            succeeded = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex is CollectorDiagnosticException detail)
                db.Audit.Add(
                    new()
                    {
                        Actor = "Collector",
                        Action = "Collector.Diagnostic",
                        Detail = CollectorDiagnosticFiles.AuditDetail(detail.Diagnostic),
                    }
                );
            await db.Entry(source).ReloadAsync(ct);
            source.LastError = SafeError(ex);
            // Exponential backoff (30 min doubling, 24 h cap); flagged for review after N failures.
            SourceHealth.RecordFailure(source, config, DateTimeOffset.UtcNow);
            run.Status = "Failed";
            run.Error = source.LastError;
            if (manual is not null)
            {
                manual.Status = "Failed";
                manual.Error = source.LastError.Truncate(1000);
                manual.FinishedAt = DateTimeOffset.UtcNow;
            }

            log.LogWarning(
                "Source {Id} collection failed ({Failures} in a row): {Type}",
                source.Id,
                source.ConsecutiveFailures,
                ex.GetType().Name
            );
        }

        run.FinishedAt = DateTimeOffset.UtcNow;
        await gate.VerifyAsync(ct);
        await db.SaveChangesAsync(ct);
        if (!succeeded)
            return;
        try
        {
            await using var healthScope = scopes.CreateAsyncScope();
            var flagged = await SourceHealth.FlagUnseenAsync(
                healthScope.ServiceProvider.GetRequiredService<HiringDb>(),
                source,
                config,
                ct
            );
            if (flagged > 0)
                log.LogInformation(
                    "Source {Id}: {Count} published records not seen in the listing moved to review.",
                    source.Id,
                    flagged
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning("Source {Id} listing check failed: {Type}", source.Id, ex.GetType().Name);
        }
    }

    private static async Task SaveDiscoveredAsync(
        HiringDb pageDb,
        int sourceId,
        DiscoveryBatch batch,
        ScrapeRun savedRun,
        int activePromptId,
        DateTimeOffset reverifyBefore,
        CancellationToken token
    )
    {
        var entries = batch.Items.DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
        var ids = entries.Select(x => x.Id).ToArray();
        // One lookup per page instead of one query per listing row.
        var existing = await pageDb
            .SourceItems.Where(x => x.SourceId == sourceId && ids.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.OrdinalIgnoreCase, token);
        var seen = DateTimeOffset.UtcNow;
        foreach (var entry in entries)
        {
            if (!existing.TryGetValue(entry.Id, out var item))
            {
                pageDb.SourceItems.Add(
                    new()
                    {
                        SourceId = sourceId,
                        ExternalId = entry.Id,
                        Url = entry.Url,
                        Status = ProcessingStatus.Discovered,
                        RequestedPromptVersionId = activePromptId,
                    }
                );
                savedRun.Changed++;
                continue;
            }
            item.LastSeenAt = seen;
            // Published items are re-fetched only once their last verification is old enough.
            if (
                item.Status == ProcessingStatus.Published
                && (item.LastVerifiedAt is null || item.LastVerifiedAt < reverifyBefore)
            )
            {
                item.Status = ProcessingStatus.Discovered;
                item.RetryAt = null;
            }
        }
    }

    private async Task ProcessPendingAsync(
        SqlRunLock gate,
        SourceCollectionRequest? manual,
        PromptVersion activePrompt,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var aiPausedUntil = await AiGovernor.PausedUntilAsync(db, now, ct);
        if (aiPausedUntil is null && await AiGovernor.BudgetExceededAsync(db, config, now, ct))
            aiPausedUntil = await PauseForBudgetAsync(now, ct);
        var pendingQuery = db
            .SourceItems.AsNoTracking()
            .Where(x =>
                x.Source.Enabled
                && x.Source.UsageApproved
                && (
                    x.Status == ProcessingStatus.Discovered
                    || x.Status == ProcessingStatus.AwaitingAi
                    || x.Status == ProcessingStatus.Retry
                )
                && (x.RetryAt == null || x.RetryAt <= now)
            );
        // While AI is paused, items still get fetched and stored, but nothing waits only on AI.
        if (aiPausedUntil is not null)
            pendingQuery = pendingQuery.Where(x => x.Status != ProcessingStatus.AwaitingAi);
        if (manual is not null)
            pendingQuery = pendingQuery.Where(x => x.SourceId == manual.SourceId);
        // New announcements first; re-verifications of known items follow.
        var pending = await pendingQuery
            .OrderBy(x => x.ExtractionPromptVersionId == null ? 0 : 1)
            .ThenBy(x => x.FirstSeenAt)
            .Select(x => x.Id)
            .Take(15)
            .ToListAsync(ct);
        foreach (var itemId in pending)
        {
            await gate.VerifyAsync(ct);
            await using var scope = scopes.CreateAsyncScope();
            var itemDb = scope.ServiceProvider.GetRequiredService<HiringDb>();
            var item = await itemDb
                .SourceItems.Include(x => x.Source)
                .SingleAsync(x => x.Id == itemId, ct);
            var itemBrowser = scope.ServiceProvider.GetRequiredService<BrowserCollector>();
            var ai = scope.ServiceProvider.GetRequiredService<AiExtractor>();
            var processor = scope.ServiceProvider.GetRequiredService<OpportunityService>();
            var stopAi = false;
            try
            {
                var linked = await itemDb
                    .SourceLinks.AsNoTracking()
                    .Include(x => x.Opportunity)
                    .Where(x => x.SourceItemId == item.Id)
                    .ToListAsync(ct);
                if (
                    linked.Count > 0
                    && linked.All(x => PromptWorkflow.Protected(x.Opportunity, activePrompt.Id))
                )
                {
                    item.Status = ProcessingStatus.Published;
                    item.RetryAt = null;
                    // Protected decisions are not re-fetched; wait a full interval before checking again.
                    item.LastVerifiedAt = DateTimeOffset.UtcNow;
                    await itemDb.SaveChangesAsync(ct);
                    continue;
                }
                if (item.Status != ProcessingStatus.AwaitingAi)
                {
                    var bundle = await itemBrowser.FetchBundleAsync(item.Source, item.Url, ct);
                    var content = bundle.Text;
                    var hash = ContentFingerprint.Hash(content);
                    await gate.VerifyAsync(ct);
                    item.LastVerifiedAt = DateTimeOffset.UtcNow;
                    // Unchanged main text with a saved extraction for this prompt: no new AI call.
                    if (
                        ContentFingerprint.Matches(item.ContentHash, content)
                        && item.ExtractionJson.Length > 0
                        && item.ExtractionPromptVersionId == activePrompt.Id
                    )
                    {
                        var saved =
                            JsonSerializer.Deserialize<ExtractionResult>(
                                item.ExtractionJson,
                                AiExtractor.JsonOptions
                            )
                            ?? throw new ContentNeedsReviewException(
                                "Saved extraction is unreadable; original text retained for review."
                            );
                        saved.PromptVersionId = item.ExtractionPromptVersionId;
                        item.ContentHash = hash;
                        await processor.ApplyExtractionAsync(item, saved, ct);
                        item.RetryAt = null;
                        item.Attempts = 0;
                        await itemDb.SaveChangesAsync(ct);
                        continue;
                    }

                    if (item.Content.Length > 0)
                        itemDb.Revisions.Add(
                            new()
                            {
                                SourceItemId = item.Id,
                                ContentHash = item.ContentHash,
                                Content = item.Content,
                            }
                        );
                    item.Content = content;
                    item.ContentHash = hash;
                    item.AttachmentsJson = JsonSerializer.Serialize(bundle.Attachments);
                    item.Status = ProcessingStatus.AwaitingAi;
                    item.ExtractionJson = "";
                    await itemDb.SaveChangesAsync(ct);
                }

                if (aiPausedUntil is null && await AiGovernor.BudgetExceededAsync(itemDb, config, DateTimeOffset.UtcNow, ct))
                    aiPausedUntil = await PauseForBudgetAsync(DateTimeOffset.UtcNow, ct);
                if (aiPausedUntil is { } pausedUntil)
                {
                    item.Status = ProcessingStatus.AwaitingAi;
                    item.RetryAt = pausedUntil;
                    item.Error =
                        $"AI paused until {SourceSchedule.Display(pausedUntil)} (Palestine time); content saved.";
                    await itemDb.SaveChangesAsync(ct);
                    continue;
                }

                var result = await ai.ExtractAsync(
                    SourceEvidence.ForItem(item),
                    ct,
                    new AiExecution(activePrompt, item.Id)
                );
                await gate.VerifyAsync(ct);
                // Save the paid result with its prompt version before applying, so a failed
                // apply reuses it instead of calling the model again.
                item.ExtractionJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
                item.ExtractionPromptVersionId = result.PromptVersionId;
                await itemDb.SaveChangesAsync(ct);
                await processor.ApplyExtractionAsync(item, result, ct);
                item.RetryAt = null;
                item.Attempts = 0;
            }
            catch (AiQuotaException ex)
            {
                // Quota or credentials: pause every AI call globally and stop this item loop.
                var until = DateTimeOffset.UtcNow + AiGovernor.QuotaPause(ex, config);
                await AiGovernor.PauseAsync(itemDb, until, ex.Message, ct);
                aiPausedUntil = until;
                stopAi = true;
                item.Status = ProcessingStatus.AwaitingAi;
                item.Error = ex.Message;
                item.RetryAt = until;
                log.LogWarning(
                    "AI paused until {Until} after provider refusal.",
                    SourceSchedule.Display(until)
                );
            }
            catch (AiUnavailableException ex)
            {
                item.Status = ProcessingStatus.AwaitingAi;
                item.Error = ex.Message;
                item.RetryAt = DateTimeOffset.UtcNow.AddHours(6);
            }
            catch (AiTransientException ex)
            {
                item.Attempts++;
                if (item.Attempts >= AiGovernor.MaxTransientAttempts(config))
                {
                    item.Status = ProcessingStatus.NeedsReview;
                    item.Error = ("AI provider kept failing; saved content retained. " + ex.Message).Truncate(1800);
                }
                else
                {
                    item.Status = ProcessingStatus.AwaitingAi;
                    item.Error = ex.Message;
                    item.RetryAt = DateTimeOffset.UtcNow + AiGovernor.TransientBackoff(item.Attempts);
                }
            }
            catch (CollectorDiagnosticException ex)
            {
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                itemDb.Audit.Add(
                    new()
                    {
                        Actor = "Collector",
                        Action = "Collector.Diagnostic",
                        Detail = CollectorDiagnosticFiles.AuditDetail(ex.Diagnostic, itemId),
                    }
                );
                if (ex.InnerException is ContentNeedsReviewException)
                    item.Status = ProcessingStatus.NeedsReview;
                else
                    ScheduleRetry(item);

                item.Error = SafeError(ex);
            }
            catch (ContentNeedsReviewException ex)
            {
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                item.Status = ProcessingStatus.NeedsReview;
                item.Error = ex.Message.Truncate(1800);
            }
            catch (JsonException)
            {
                // Saved or returned extraction cannot be read; repeating it would not change that.
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                item.Status = ProcessingStatus.NeedsReview;
                item.Error = "Extraction JSON is unreadable; original text retained for review.";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                ScheduleRetry(item);
                item.Error = SafeError(ex);
            }

            await gate.VerifyAsync(ct);
            try
            {
                await itemDb.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // One item's failed status save (e.g. a concurrent admin edit) must not stop the tick.
                log.LogWarning("Item {Id} status save failed: {Type}", itemId, ex.GetType().Name);
            }
            if (stopAi)
                break;
        }
    }

    private async Task<DateTimeOffset> PauseForBudgetAsync(DateTimeOffset now, CancellationToken ct)
    {
        var until = AiGovernor.NextPalestineDay(now);
        await AiGovernor.PauseAsync(
            db,
            until,
            $"Daily AI token budget ({AiGovernor.DailyTokenBudget(config)}) reached.",
            ct
        );
        log.LogWarning(
            "Daily AI token budget reached; AI paused until {Until}.",
            SourceSchedule.Display(until)
        );
        return until;
    }

    private static void ScheduleRetry(SourceItem item)
    {
        item.Attempts++;
        item.Status = item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
        item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(240, 10 * item.Attempts));
    }

    private static string SafeError(Exception ex) =>
        ex switch
        {
            CollectorDiagnosticException diagnostic => SafeError(diagnostic.InnerException!)
                + $" Diagnostic ID: {diagnostic.Diagnostic.Id:N}",
            ContentNeedsReviewException => ex.Message.Truncate(1600),
            ContentRetryableException => ex.Message.Truncate(1600),
            InvalidOperationException => ex.Message.Truncate(1800),
            HttpRequestException http =>
                $"Source HTTP {(http.StatusCode is { } code ? ((int)code).ToString() : "network error")}; retry scheduled; no source closure inferred.",
            TimeoutException =>
                "Source page timed out before required content became ready; retry scheduled; no source closure inferred.",
            OperationCanceledException =>
                "Source download/request timed out or was cancelled; retry scheduled; no source closure inferred.",
            _ => $"{ex.GetType().Name}: inspect server logs; no source closure inferred.",
        };
}
