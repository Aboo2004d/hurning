// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون OpportunityService ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public class OpportunityService(HiringDb db, OpportunityExtractionService? extractionService = null)
{
    private readonly OpportunityExtractionService extraction = extractionService ?? new(db);

    // Validation and duplicate rules live in ExtractionValidator/DuplicateMatcher; these keep callers stable.
    /// <summary>Validation and duplicate rules live in ExtractionValidator/DuplicateMatcher; these keep callers stable.</summary>
    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent) =>
        ExtractionValidator.VerifiedOptionalApplyUrl(value, sourceContent);

    /// <summary>ينفذ IsWhatsAppHost وفق قواعد OpportunityService الحالية دون تغيير عقد المستدعين.</summary>
    public static bool IsWhatsAppHost(string host) => ExtractionValidator.IsWhatsAppHost(host);

    /// <summary>ينفذ ResolveApplyUrl وفق قواعد OpportunityService الحالية دون تغيير عقد المستدعين.</summary>
    public static string ResolveApplyUrl(string? aiUrl, SourceItem item) =>
        ExtractionValidator.ResolveApplyUrl(aiUrl, item);

    /// <summary>يتحقق من حقول الاستخراج ونطاقه وروابطه وأدلته دون حفظه.</summary>
    public static List<string> Validate(
        Opportunity o,
        string? sourceContent = null,
        bool allowUnrecognizedLocation = false,
        IEnumerable<string>? approvedLocations = null
    ) =>
        ExtractionValidator.Validate(o, sourceContent, allowUnrecognizedLocation, approvedLocations);

    /// <summary>ينفذ DateEvidenceMatches وفق قواعد OpportunityService الحالية دون تغيير عقد المستدعين.</summary>
    public static bool DateEvidenceMatches(string evidence, DateOnly claimed) =>
        ExtractionValidator.DateEvidenceMatches(evidence, claimed);

    /// <summary>ينفذ PotentialDuplicate وفق قواعد OpportunityService الحالية دون تغيير عقد المستدعين.</summary>
    public static bool PotentialDuplicate(Opportunity incoming, Opportunity existing) =>
        DuplicateMatcher.PotentialDuplicate(incoming, existing);

    /// <summary>يفوض تطبيق الاستخراج إلى الخدمة المختصة دون تغيير العقد العام.</summary>
    public Task ApplyExtractionAsync(SourceItem item, ExtractionResult result, CancellationToken ct) =>
        extraction.ApplyExtractionAsync(item, result, ct);

    /// <summary>يؤرشف الفرص المنشورة التي انتهت وفق الموعد المثبت.</summary>
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
