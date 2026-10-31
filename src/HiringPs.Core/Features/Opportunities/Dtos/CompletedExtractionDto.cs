namespace HiringPs.Core;
/// <summary>نسخة العمل المستكملة مع أسماء الحقول المصححة والتحذيرات؛ يبقى رد AI الأصلي محفوظًا.</summary>
public sealed record CompletedExtractionDto(ExtractedOpportunity Value, string[] Fields, string[] Warnings);
