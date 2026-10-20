namespace HiringPs.Core;

/// <summary>خصم مؤهل مع معرّفه ونسبته فقط؛ الحجز الفعلي يتم ضمن معاملة إنشاء الطلب.</summary>
public sealed record MembershipDiscountDto(int Id, int Percent);
