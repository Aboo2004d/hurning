namespace HiringPs.Core;

/// <summary>تعديل خصم إداري مع هوية الفاعل؛ يمنع تغيير الشروط المرتبطة بطلبات سابقة.</summary>
public sealed record DiscountSaveCommandDto(int Id, string Code, int? PlanId, int Percent,
    DateTimeOffset ExpiresAt, int MaximumUses, bool Enabled, string Actor);
