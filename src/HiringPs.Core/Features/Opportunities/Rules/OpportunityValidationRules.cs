using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قواعد نقية للتحقق من الفرص وروابط التقديم والأدلة والتكرار دون قاعدة بيانات.</summary>
public static class OpportunityValidationRules
{
    /// <summary>ينفذ VerifiedOptionalApplyUrl وفق قواعد OpportunityValidationRules الحالية دون تغيير عقد المستدعين.</summary>
    public static string VerifiedOptionalApplyUrl(string? value, string sourceContent) =>
        ExtractionValidator.VerifiedOptionalApplyUrl(value, sourceContent);

    /// <summary>ينفذ IsWhatsAppHost وفق قواعد OpportunityValidationRules الحالية دون تغيير عقد المستدعين.</summary>
    public static bool IsWhatsAppHost(string host) => ExtractionValidator.IsWhatsAppHost(host);

    /// <summary>ينفذ ResolveApplyUrl وفق قواعد OpportunityValidationRules الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ DateEvidenceMatches وفق قواعد OpportunityValidationRules الحالية دون تغيير عقد المستدعين.</summary>
    public static bool DateEvidenceMatches(string evidence, DateOnly claimed) =>
        ExtractionValidator.DateEvidenceMatches(evidence, claimed);

    /// <summary>ينفذ PotentialDuplicate وفق قواعد OpportunityValidationRules الحالية دون تغيير عقد المستدعين.</summary>
    public static bool PotentialDuplicate(Opportunity incoming, Opportunity existing) =>
        DuplicateMatcher.PotentialDuplicate(incoming, existing);

}
