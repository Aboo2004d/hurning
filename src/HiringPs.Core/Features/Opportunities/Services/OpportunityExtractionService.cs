using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يطبق نتائج AI داخل معاملة مع حماية التعديلات وأدلة التواريخ والتكرار.</summary>
public sealed class OpportunityExtractionService(HiringDb db)
{
    /// <summary>يطبق الاستخراج على الفرص وروابط أدلتها مع حماية تعديلات الإدارة.</summary>
    public async Task ApplyExtractionAsync(
        SourceItem item,
        ExtractionResult result,
        CancellationToken ct
    )
    {
        if (result.Opportunities is null)
            throw new InvalidOperationException("AI returned no opportunity array.");
        foreach (var value in result.Opportunities)
        foreach (
            var property in typeof(ExtractedOpportunity)
                .GetProperties()
                .Where(p => p.PropertyType == typeof(string))
        )
            if (property.GetValue(value) is null)
                property.SetValue(value, "");
        foreach (var opportunity in result.Opportunities)
            if (opportunity.ChildKey.Length > 100)
                opportunity.ChildKey = TextRules.Hash(opportunity.ChildKey);
        if (!result.InScope)
        {
            item.Status = ProcessingStatus.NeedsReview;
            item.Error = "AI marked out of scope; verify before excluding.";
            await db.SaveChangesAsync(ct);
            return;
        }

        if (result.Opportunities.Count is 0 or > 30)
            throw new InvalidOperationException("Invalid extracted opportunity count.");
        if (
            result.Opportunities.Any(x =>
                string.IsNullOrWhiteSpace(x.ChildKey) || x.ChildKey.Length > 100
            )
            || result.Opportunities.Select(x => x.ChildKey).Distinct().Count()
                != result.Opportunities.Count
        )
            throw new InvalidOperationException(
                "Missing/duplicate stable child keys; review source."
            );
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var allReviewed = true;
        var reviewReasons = new List<string>();
        var approvedLocations = await LocationCatalog.ApprovedAsync(db, ct);
        var sourceContent = SourceEvidence.ForItem(item);
        foreach (var e in result.Opportunities)
        {
            var link = await db
                .SourceLinks.Include(x => x.Opportunity)
                .SingleOrDefaultAsync(
                    x => x.SourceItemId == item.Id && x.ChildKey == e.ChildKey,
                    ct
                );
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
            candidate.ApplyUrl = OpportunityValidationRules.ResolveApplyUrl(e.ApplyUrl, item);
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
            var errors = OpportunityValidationRules.Validate(
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
            var activeRules = result.PromptVersionId is { } versionId
                ? await db.PromptVersions.AsNoTracking().SingleAsync(x => x.Id == versionId, ct)
                : null;
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
                && !OpportunityValidationRules.DateEvidenceMatches(e.DeadlineEvidence, claimed)
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
            var exact = await db.Opportunities.FirstOrDefaultAsync(
                x =>
                    x.Fingerprint == candidate.Fingerprint
                    && x.Id != (link == null ? 0 : link.OpportunityId),
                ct
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
                    exact.LastVerifiedAt = DateTimeOffset.UtcNow;
                continue;
            }

            if (exact is not null)
                errors.Add(
                    "Possible duplicate without a unique reference; administrator review required."
                );
            if (exact is null && link is null)
            {
                var normalizedOrganization = TextRules.OrganizationKey(candidate.Organization);
                var peers = await db
                    .Opportunities.AsNoTracking()
                    .Where(x =>
                        x.Kind == candidate.Kind
                        && x.NormalizedOrganization == normalizedOrganization
                        && (
                            x.Status == PublicationStatus.Published
                            || x.Status == PublicationStatus.NeedsReview
                            || x.Status == PublicationStatus.Archived
                        )
                    )
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .ToListAsync(ct);
                if (peers.Any(x => OpportunityValidationRules.PotentialDuplicate(candidate, x)))
                    errors.Add(
                        "Possible repost or similar role across sources; compare and merge in administration if identical."
                    );
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

            target.PromptVersionId = result.PromptVersionId;
            foreach (var date in checkedDates.Dates)
            {
                var evidenceHash = TextRules.Hash(
                    $"{date.Kind}|{date.Date:yyyy-MM-dd}|{date.Label}|{date.Evidence}"
                );
                var exists =
                    target.Id != 0
                    && await db.OpportunityDates.AnyAsync(
                        x =>
                            x.OpportunityId == target.Id
                            && x.Kind == date.Kind
                            && x.EvidenceHash == evidenceHash,
                        ct
                    );
                if (!exists)
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
            target.LastVerifiedAt = DateTimeOffset.UtcNow;
            target.ReviewReason = string.Join(" ", errors);
            target.Status =
                errors.Count > 0 ? PublicationStatus.NeedsReview
                : target.Deadline is { } deadline && deadline < TextRules.PalestineToday()
                    ? PublicationStatus.Archived
                : PublicationStatus.Published;
            if (target.Status == PublicationStatus.Archived)
                target.ClosureEvidence = "Published source deadline has passed.";
            if (errors.Count > 0)
                allReviewed = false;
            reviewReasons.AddRange(errors);
        }

        var currentKeys = result.Opportunities.Select(x => x.ChildKey).ToArray();
        var removed = await db
            .SourceLinks.Include(x => x.Opportunity)
            .Where(x => x.SourceItemId == item.Id && !currentKeys.Contains(x.ChildKey))
            .ToListAsync(ct);
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
        await tx.CommitAsync(ct);
    }

}
