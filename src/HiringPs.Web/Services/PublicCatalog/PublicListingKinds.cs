using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مجموعات التصفح العامة؛ يبقى نوع الفرصة الأصلي محفوظًا للبطاقة والصلاحيات.</summary>
public static class PublicListingKinds
{
    public static IReadOnlyList<OpportunityKind> Tabs { get; } =
        Array.AsReadOnly(new[] { OpportunityKind.Job, OpportunityKind.Training, OpportunityKind.Tender });

    /// <summary>يوحد العطاءات والمناقصات في التصفح فقط.</summary>
    public static OpportunityKind? Group(OpportunityKind? kind) =>
        kind == OpportunityKind.ProcurementNotice ? OpportunityKind.Tender : kind;

    /// <summary>مفتاح عنوان مجموعة التصفح، لا شارة نوع الفرصة.</summary>
    public static string Label(OpportunityKind? kind) => Group(kind) switch
    {
        OpportunityKind.Job => "Jobs",
        OpportunityKind.Tender => "TendersAndProcurement",
        null => "Headline",
        _ => kind.ToString()!,
    };

    /// <summary>يطبق المجموعة داخل SQL مع إبقاء التدريب والوظائف منفصلين.</summary>
    public static IQueryable<Opportunity> Apply(IQueryable<Opportunity> query, OpportunityKind? kind) =>
        Group(kind) switch
        {
            OpportunityKind.Tender => query.Where(x => x.Kind == OpportunityKind.Tender || x.Kind == OpportunityKind.ProcurementNotice),
            null => query,
            _ => query.Where(x => x.Kind == kind),
        };
}
