// قواعد مشتركة للصفحات العامة: حجم الصفحة، الفرص المنشورة الحالية، روابط التفاصيل والـslug.
using System.Globalization;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون KindSlugs ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class KindSlugs
{
    /// <summary>ينفذ For وفق قواعد KindSlugs الحالية دون تغيير عقد المستدعين.</summary>
    public static string For(OpportunityKind kind) =>
        kind switch
        {
            OpportunityKind.Job => "jobs",
            OpportunityKind.Training => "training",
            OpportunityKind.Tender => "tenders",
            _ => "procurement",
        };

    /// <summary>ينفذ Parse وفق قواعد KindSlugs الحالية دون تغيير عقد المستدعين.</summary>
    public static OpportunityKind? Parse(string? slug) =>
        slug?.ToLowerInvariant() switch
        {
            "jobs" => OpportunityKind.Job,
            "training" => OpportunityKind.Training,
            "tenders" => OpportunityKind.Tender,
            "procurement" => OpportunityKind.ProcurementNotice,
            _ => null,
        };

    public const string RouteConstraint = "regex(^(jobs|training|tenders|procurement)$)";
}

/// <summary>Readable URL segments; keeps Arabic and Latin letters/digits and joins words with '-'.</summary>
