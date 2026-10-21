namespace HiringPs.Core;

/// <summary>حقول تعديل الباقة المعتمدة؛ كل حفظ ينشئ نسخة شروط جديدة ولا يعدل المشتريات السابقة.</summary>
public sealed record PlanSaveCommandDto(int? Id, string NameAr, string NameEn,
    string DescriptionAr, string DescriptionEn, string BadgeAr, string BadgeEn,
    decimal PriceIls, int Days, OpportunityKind[] Details, OpportunityKind[] Alerts,
    bool Visible, int SortOrder, string? Version, string Actor, int? MaxPreferredCategories = null);
