namespace HiringPs.Core;

/// <summary>نوع فرص حالي وآخر تغير محتوى ضمنه لخريطة صفحات الموقع.</summary>
public sealed record OpportunityKindChangeDto(OpportunityKind Kind, DateTimeOffset Changed);
