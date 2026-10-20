// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يطبق الاستخراج وأدلته مع قواعد التكرار وحماية الدمج والاعتماد الإداري داخل المعاملة.</summary>
public sealed class OpportunityExtractionService(HiringDb db)
{
    /// <summary>يطبق البيانات والأدلة وقواعد الدمج والتكرار ضمن المعاملة الحالية.</summary>
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

}
