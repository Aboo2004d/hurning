namespace HiringPs.Core;

/// <summary>حقول سجل النشر اللازمة للعرض فقط دون نص المنشور أو بيانات الفاعل.</summary>
public sealed record SocialPublicationSummaryDto(string Provider, DateTimeOffset CreatedAt, string Status, string Url);
