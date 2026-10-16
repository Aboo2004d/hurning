// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public record ExtractionResult(bool InScope, List<ExtractedOpportunity> Opportunities)
{
    [JsonIgnore]
    public int? PromptVersionId { get; set; }
}

public class ExtractedOpportunity
{
    public string ChildKey { get; set; } = "";
    public OpportunityKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
    public string OriginalLanguage { get; set; } = "ar";
    public string Summary { get; set; } = "";
    public string Locations { get; set; } = "";
    public string Category { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string WorkMode { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Eligibility { get; set; } = "";
    public string Cost { get; set; } = "";
    public string ApplyUrl { get; set; } = "";
    public DateOnly? Deadline { get; set; }
    public string DeadlineEvidence { get; set; } = "";
    public DateOnly? PublicationDate { get; set; }
    public string PublicationEvidence { get; set; } = "";
    public DateOnly? ModificationDate { get; set; }
    public string ModificationEvidence { get; set; } = "";
    public bool DeadlineIsExtension { get; set; }
    public List<ExtractedDate> OtherDates { get; set; } = [];
    public string LocationEvidence { get; set; } = "";
    public bool? IsInPalestine { get; set; }
    public string ReviewReason { get; set; } = "";
}

public class AiUnavailableException(string message) : Exception(message);

public class AiTransientException(string message) : Exception(message);

public class AiExtractor
{
    private readonly HttpClient client;
    private readonly IConfiguration config;
    private readonly IServiceScopeFactory? scopes;
    private readonly ILogger<AiExtractor>? logger;

    public AiExtractor(
        HttpClient client,
        IConfiguration config,
        IServiceScopeFactory? scopes = null,
        ILogger<AiExtractor>? logger = null
    )
    {
        this.client = client;
        this.config = config;
        this.scopes = scopes;
        this.logger = logger;
        client.Timeout = TimeSpan.FromSeconds(
            Math.Clamp(config.GetValue<int?>("Ai:TimeoutSeconds") ?? 180, 30, 300)
        );
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ExtractionResult> ExtractAsync(
        string content,
        CancellationToken ct,
        AiExecution? execution = null
    )
    {
        var telemetry = new AiTelemetry();
        long? usageId = null;
        var started = System.Diagnostics.Stopwatch.StartNew();
        if (scopes is not null && execution is not null)
            usageId = await AiUsageRecorder.StartAsync(
                scopes,
                execution,
                config["Ai:Model"] ?? "gemini-3.5-flash-lite",
                ct
            );
        string? error = null;
        try
        {
            var result = await SendAsync(content, execution, telemetry, ct);
            result.PromptVersionId = execution?.Version.Id;
            return result;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            throw;
        }
        finally
        {
            if (usageId is { } id && scopes is not null)
            {
                try
                {
                    await AiUsageRecorder.FinishAsync(
                        scopes,
                        id,
                        telemetry,
                        error,
                        started.ElapsedMilliseconds
                    );
                }
                catch (Exception)
                {
                    logger?.LogWarning(
                        "AI usage record {Id} remains unfinished; result was not discarded.",
                        id
                    );
                }
            }
        }
    }

    private async Task<ExtractionResult> SendAsync(
        string content,
        AiExecution? execution,
        AiTelemetry telemetry,
        CancellationToken ct
    )
    {
        var key = config["Ai:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("AI key is not configured; item remains queued.");
        var model = config["Ai:Model"] ?? "gemini-3.5-flash-lite";
        if (!System.Text.RegularExpressions.Regex.IsMatch(model, @"\A[a-zA-Z0-9._-]+\z"))
            throw new InvalidOperationException("Invalid model name.");
        var prompt = execution?.Version ?? PromptDefaults.Initial();
        var aiInstructions = prompt.Render();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        );
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(
            new
            {
                systemInstruction = new { parts = new[] { new { text = aiInstructions } } },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = "SOURCE DATA:\n" + content } },
                    },
                },
                generationConfig = new { temperature = 0, responseMimeType = "application/json" },
            }
        );
        HttpResponseMessage received;
        try
        {
            received = await client.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiTransientException(
                $"AI request timed out after {client.Timeout.TotalSeconds:0} seconds; retry scheduled, saved content retained."
            );
        }
        catch (HttpRequestException)
        {
            throw new AiTransientException(
                "AI network connection failed; retry scheduled, saved content retained."
            );
        }

        using var response = received;
        telemetry.HttpStatus = (int)response.StatusCode;
        if (
            response.StatusCode
            is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.RequestTimeout
        )
            throw new AiTransientException(
                $"AI HTTP {(int)response.StatusCode}: temporary provider error; retry scheduled, saved content retained."
            );
        if (
            response.StatusCode == HttpStatusCode.TooManyRequests
            || response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden
        )
            throw new AiUnavailableException(
                $"AI HTTP {(int)response.StatusCode}: quota/credentials unavailable; no paid fallback used."
            );
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Inspect only for a limit classification; never store the provider body or echoed source/key.
            var body = await response.Content.ReadAsStringAsync(ct);
            if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    body,
                    @"(?is)(?:input token|context length|context window).{0,160}(?:exceed|maximum|limit)|(?:exceed|too many).{0,120}(?:input token|context length)"
                )
            )
                throw new ContentNeedsReviewException(
                    "AI input exceeds the provider context limit; original text is saved. Use a model with a larger supported context or review manually."
                );
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            throw new AiUnavailableException(
                $"AI HTTP {(int)response.StatusCode}: check configured model {model} and request compatibility."
            );
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        telemetry.Read(result.RootElement);
        var text = result
            .RootElement.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        return JsonSerializer.Deserialize<ExtractionResult>(text ?? "", JsonOptions)
            ?? throw new InvalidOperationException("Empty AI result.");
    }
}

public class OpportunityService(HiringDb db)
{
    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent)
    {
        var url = value?.Trim() ?? "";
        return
            url.Length is > 0 and <= 1500
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "mailto" or "tel"
            && (
                uri.Scheme != "https"
                || (string.IsNullOrEmpty(uri.UserInfo) && !IsWhatsAppHost(uri.IdnHost))
            )
            && sourceContent.Contains(url, StringComparison.OrdinalIgnoreCase)
            ? url
            : "";
    }

    public static bool IsWhatsAppHost(string host) =>
        host.Equals("wa.me", StringComparison.OrdinalIgnoreCase)
        || host.Equals("whatsapp.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".whatsapp.com", StringComparison.OrdinalIgnoreCase);

    public static string ResolveApplyUrl(string? aiUrl, SourceItem item)
    {
        // Always identify the actual collected announcement, never an inferred contact link.
        var originalUrl = item.Url?.Trim() ?? "";
        return
            Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && string.IsNullOrEmpty(uri.UserInfo)
            ? originalUrl
            : "";
    }

    public static List<string> Validate(
        Opportunity o,
        string? sourceContent = null,
        bool allowUnrecognizedLocation = false,
        IEnumerable<string>? approvedLocations = null
    )
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(o.Title))
            errors.Add("Title required.");
        if (string.IsNullOrWhiteSpace(o.Organization))
            errors.Add("Organization required.");
        if (string.IsNullOrWhiteSpace(o.Summary))
            errors.Add("Summary required.");
        if (!Enum.IsDefined(o.Kind))
            errors.Add("Invalid opportunity kind.");
        if (o.OriginalLanguage is not ("ar" or "en"))
            errors.Add("Language must be ar or en.");
        if (
            o.Kind != OpportunityKind.Job
            && (o.EmploymentType.Length > 0 || o.ExperienceLevel.Length > 0)
        )
            errors.Add("Employment fields belong to jobs only.");
        if (o.Locations.Length == 0)
            errors.Add("Location needs review.");
        var localPlaces = new[]
        {
            "غزة",
            "القدس",
            "رام الله",
            "البيرة",
            "الخليل",
            "نابلس",
            "جنين",
            "بيت لحم",
            "أريحا",
            "اريحا",
            "طولكرم",
            "قلقيلية",
            "طوباس",
            "سلفيت",
            "فلسطين",
            "الضفة",
            "Gaza",
            "Jerusalem",
            "Ramallah",
            "Hebron",
            "Nablus",
            "Jenin",
            "Bethlehem",
            "Jericho",
            "Tulkarm",
            "Qalqilya",
            "Tubas",
            "Salfit",
            "Palestine",
            "West Bank",
        };
        if (
            !allowUnrecognizedLocation
            && !localPlaces.Any(place =>
                o.Locations.Contains(place, StringComparison.OrdinalIgnoreCase)
            )
            && !LocationCatalog.Matches(o.Locations, approvedLocations ?? [])
        )
            errors.Add("Local geographic scope needs review.");
        if (
            o.ApplyUrl.Length > 0
            && (
                !Uri.TryCreate(o.ApplyUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("https" or "mailto" or "tel")
            )
        )
            errors.Add("Invalid application link.");
        if (sourceContent is not null && !TextEvidence.TitleContains(sourceContent, o.Title))
            errors.Add("Original title not found in source text.");
        return errors;
    }

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
            candidate.ApplyUrl = ResolveApplyUrl(e.ApplyUrl, item);
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
            var errors = Validate(
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
                && !DateEvidenceMatches(e.DeadlineEvidence, claimed)
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
                if (peers.Any(x => PotentialDuplicate(candidate, x)))
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

    public static bool DateEvidenceMatches(string evidence, DateOnly claimed)
    {
        evidence = string.Concat(
            TextEvidence
                .Normalize(evidence)
                .Select(c =>
                    char.GetUnicodeCategory(c)
                    == System.Globalization.UnicodeCategory.DecimalDigitNumber
                        ? ((int)char.GetNumericValue(c)).ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                        : c.ToString()
                )
        );
        foreach (
            System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                evidence,
                @"(?<![\d/\-])(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?![\d/\-])"
            )
        )
            if (
                DateOnly.TryParseExact(
                    match.Value,
                    new[]
                    {
                        "yyyy-MM-dd",
                        "yyyy-M-d",
                        "yyyy/MM/dd",
                        "yyyy/M/d",
                        "dd/MM/yyyy",
                        "d/M/yyyy",
                        "dd-MM-yyyy",
                        "d-M-yyyy",
                    },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed
                )
                && parsed == claimed
            )
                return true;
        // Extract the date itself: the evidence can include a weekday, deadline label and time.
        string[] monthNames =
        [
            "January|Jan|يناير|كانون الثاني",
            "February|Feb|فبراير|شباط",
            "March|Mar|مارس|اذار",
            "April|Apr|ابريل|نيسان",
            "May|مايو|ايار",
            "June|Jun|يونيو|حزيران",
            "July|Jul|يوليو|تموز",
            "August|Aug|اغسطس|اب",
            "September|Sept|Sep|سبتمبر|ايلول",
            "October|Oct|اكتوبر|تشرين الاول",
            "November|Nov|نوفمبر|تشرين الثاني",
            "December|Dec|ديسمبر|كانون الاول",
        ];
        for (var month = 1; month <= monthNames.Length; month++)
        {
            var name = "(?:" + monthNames[month - 1] + ")";
            var pattern =
                @"\b(?:(?<day>\d{1,2})\s+"
                + name
                + @"\.?[\s,،]+(?<year>\d{4})|"
                + name
                + @"\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?[\s,،]+(?<year>\d{4}))\b";
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    evidence,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                )
            )
                if (
                    int.Parse(
                        match.Groups["day"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    ) == claimed.Day
                    && int.Parse(
                        match.Groups["year"].Value,
                        System.Globalization.CultureInfo.InvariantCulture
                    ) == claimed.Year
                    && month == claimed.Month
                )
                    return true;
        }

        foreach (
            var culture in new[]
            {
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.CultureInfo.GetCultureInfo("ar-PS"),
            }
        )
            if (
                DateOnly.TryParse(
                    evidence,
                    culture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                    out var parsed
                )
                && parsed == claimed
            )
                return true;
        return false;
    }

    public static bool PotentialDuplicate(Opportunity incoming, Opportunity existing)
    {
        if (
            incoming.Kind != existing.Kind
            || TextRules.Normalize(incoming.Organization)
                != TextRules.Normalize(existing.Organization)
        )
            return false;
        if (incoming.ReferenceNumber.Length > 0 && existing.ReferenceNumber.Length > 0)
            return TextRules.Normalize(incoming.ReferenceNumber)
                == TextRules.Normalize(existing.ReferenceNumber);
        var a = TextRules
            .Normalize(incoming.Title)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        var b = TextRules
            .Normalize(existing.Title)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        if (a.Count == 0 || b.Count == 0)
            return false;
        return (double)a.Intersect(b).Count() / a.Union(b).Count() >= 0.6;
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
    public async Task TickAsync(CancellationToken ct)
    {
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct))
            return;
        var activePrompt = await PromptWorkflow.ActivateAsync(db, ct);
        await RetentionCleanup.RunAsync(db, config, ct);
        var now = DateTimeOffset.UtcNow;
        var heartbeat = await db.Heartbeats.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (heartbeat is null)
        {
            heartbeat = new();
            db.Heartbeats.Add(heartbeat);
        }

        heartbeat.At = now;
        heartbeat.State = "Running";
        await db.SaveChangesAsync(ct);
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
        {
            await gate.VerifyAsync(ct);
            var run = new ScrapeRun { SourceId = source.Id };
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
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
                        foreach (var entry in batch.Items.DistinctBy(x => x.Id))
                        {
                            var item = await pageDb.SourceItems.SingleOrDefaultAsync(
                                x => x.SourceId == source.Id && x.ExternalId == entry.Id,
                                token
                            );
                            if (item is null)
                            {
                                pageDb.SourceItems.Add(
                                    new()
                                    {
                                        SourceId = source.Id,
                                        ExternalId = entry.Id,
                                        Url = entry.Url,
                                        Status = ProcessingStatus.Discovered,
                                        RequestedPromptVersionId = activePrompt.Id,
                                    }
                                );
                                savedRun.Changed++;
                            }
                            else
                            {
                                item.LastSeenAt = DateTimeOffset.UtcNow;
                                if (item.Status == ProcessingStatus.Published)
                                {
                                    item.Status = ProcessingStatus.Discovered;
                                    item.RetryAt = null;
                                }
                            }
                        }
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
                source.ScheduleAnchorDate ??= SourceSchedule.LocalDate(finished);
                if (manual is null || source.NextDueAt <= finished)
                    source.NextDueAt = SourceSchedule.NextAfter(source, finished);
                if (manual is not null)
                    manual.DiscoveryFinished = true;
                run.Status = "Success";
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
                source.CollectionRetryAt = DateTimeOffset.UtcNow.AddMinutes(30);
                run.Status = "Failed";
                run.Error = source.LastError;
                if (manual is not null)
                {
                    manual.Status = "Failed";
                    manual.Error = source.LastError.Truncate(1000);
                    manual.FinishedAt = DateTimeOffset.UtcNow;
                }

                log.LogWarning(
                    "Source {Id} collection failed: {Type}",
                    source.Id,
                    ex.GetType().Name
                );
            }

            run.FinishedAt = DateTimeOffset.UtcNow;
            await gate.VerifyAsync(ct);
            await db.SaveChangesAsync(ct);
        }

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
        if (manual is not null)
            pendingQuery = pendingQuery.Where(x => x.SourceId == manual.SourceId);
        var pending = await pendingQuery
            .OrderBy(x => x.FirstSeenAt)
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
                    await itemDb.SaveChangesAsync(ct);
                    continue;
                }
                if (item.Status != ProcessingStatus.AwaitingAi)
                {
                    var bundle = await itemBrowser.FetchBundleAsync(item.Source, item.Url, ct);
                    var content = bundle.Text;
                    var hash = TextRules.Hash(content);
                    await gate.VerifyAsync(ct);
                    if (
                        item.ContentHash == hash
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
                        await processor.ApplyExtractionAsync(item, saved, ct);
                        item.RetryAt = null;
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

                var result = await ai.ExtractAsync(
                    SourceEvidence.ForItem(item),
                    ct,
                    new AiExecution(activePrompt, item.Id)
                );
                await gate.VerifyAsync(ct);
                item.ExtractionJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
                await itemDb.SaveChangesAsync(ct);
                await processor.ApplyExtractionAsync(item, result, ct);
                item.RetryAt = null;
            }
            catch (AiUnavailableException ex)
            {
                item.Status = ProcessingStatus.AwaitingAi;
                item.Error = ex.Message;
                item.RetryAt = DateTimeOffset.UtcNow.AddHours(6);
            }
            catch (AiTransientException ex)
            {
                item.Status = ProcessingStatus.AwaitingAi;
                item.Error = ex.Message;
                item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(15);
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
                {
                    item.Attempts++;
                    item.Status =
                        item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
                    item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(
                        Math.Min(240, 10 * item.Attempts)
                    );
                }

                item.Error = SafeError(ex);
            }
            catch (ContentNeedsReviewException ex)
            {
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                item.Status = ProcessingStatus.NeedsReview;
                item.Error = ex.Message;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                itemDb.ChangeTracker.Clear();
                item = await itemDb.SourceItems.SingleAsync(x => x.Id == itemId, ct);
                item.Attempts++;
                item.Status =
                    item.Attempts >= 5 ? ProcessingStatus.NeedsReview : ProcessingStatus.Retry;
                item.RetryAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(240, 10 * item.Attempts));
                item.Error = SafeError(ex);
            }

            await gate.VerifyAsync(ct);
            await itemDb.SaveChangesAsync(ct);
        }

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
        heartbeat.At = DateTimeOffset.UtcNow;
        heartbeat.State = "Idle";
        await db.SaveChangesAsync(ct);
    }

    private static string SafeError(Exception ex) =>
        ex switch
        {
            CollectorDiagnosticException diagnostic => SafeError(diagnostic.InnerException!)
                + $" Diagnostic ID: {diagnostic.Diagnostic.Id:N}",
            ContentNeedsReviewException => ex.Message.Truncate(1600),
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
