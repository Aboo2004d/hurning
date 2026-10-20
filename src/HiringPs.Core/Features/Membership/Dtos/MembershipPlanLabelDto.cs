namespace HiringPs.Core;

/// <summary>هوية نسخة باقة واسمها ثنائي اللغة لاستخدامها في قوائم الاختيار والسجلات.</summary>
public sealed record MembershipPlanLabelDto(int Id, string NameAr, string NameEn)
{
    /// <summary>يختار الاسم حسب لغة الواجهة بنفس سياسة أسماء الباقات الحالية.</summary>
    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
