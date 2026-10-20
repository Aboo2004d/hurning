using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>واجهة الفرص المتوافقة؛ تفوض الاستخراج وقواعد التحقق وتنسق أرشفة المنتهي.</summary>
public class OpportunityService(HiringDb db, OpportunityExtractionService? extractionService = null)
{
    private readonly OpportunityExtractionService extraction = extractionService ?? new(db);

    /// <summary>يتحقق من رابط تقديم اختياري مثبت في النص.</summary>
    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent) =>
        OpportunityValidationRules.VerifiedOptionalApplyUrl(value, sourceContent);

    /// <summary>يميز نطاقات واتساب عند فحص رابط التقديم.</summary>
    public static bool IsWhatsAppHost(string host) => OpportunityValidationRules.IsWhatsAppHost(host);

    /// <summary>يعيد رابط الإعلان الأصلي المقبول.</summary>
    public static string ResolveApplyUrl(string? aiUrl, SourceItem item) =>
        OpportunityValidationRules.ResolveApplyUrl(aiUrl, item);

    /// <summary>يفحص حقول الفرصة وأدلتها ونطاقها الجغرافي.</summary>
    public static List<string> Validate(Opportunity o, string? sourceContent = null,
        bool allowUnrecognizedLocation = false, IEnumerable<string>? approvedLocations = null) =>
        OpportunityValidationRules.Validate(o, sourceContent, allowUnrecognizedLocation, approvedLocations);

    /// <summary>يطبق الاستخراج بمعاملته وقواعد حماية التعديلات الحالية.</summary>
    public Task ApplyExtractionAsync(SourceItem item, ExtractionResult result, CancellationToken ct) =>
        extraction.ApplyExtractionAsync(item, result, ct);

    /// <summary>يتحقق من أن الاقتباس يثبت الموعد المحدد.</summary>
    public static bool DateEvidenceMatches(string evidence, DateOnly claimed) =>
        OpportunityValidationRules.DateEvidenceMatches(evidence, claimed);

    /// <summary>يحدد التشابه الذي يحتاج مراجعة دون دمج تلقائي.</summary>
    public static bool PotentialDuplicate(Opportunity incoming, Opportunity existing) =>
        OpportunityValidationRules.PotentialDuplicate(incoming, existing);

    /// <summary>يؤرشف الفرص التي انتهت وفق قواعد الموعد الحالية.</summary>
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
