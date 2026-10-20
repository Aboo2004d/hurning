namespace HiringPs.Core;

/// <summary>Prepares one extraction candidate and its evidence errors without database or provider calls.</summary>
internal static class ExtractionCandidateRules
{
    /// <summary>Builds the candidate, verifies quoted dates and fields, and retains explicit-extension protection.</summary>
    internal static PreparedExtractionCandidate Prepare(
        SourceItem item,
        ExtractedOpportunity e,
        Opportunity? previous,
        string sourceContent,
        List<string> approvedLocations,
        PromptVersion? activeRules,
        bool singleOpportunity
    )
    {
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
            && singleOpportunity
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
            previous?.Deadline is { } previousDeadline
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
        return new(candidate, checkedDates, errors);
    }
}
