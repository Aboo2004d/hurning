namespace HiringPs.Core;

/// <summary>بيانات تجهيز منشور القناة من القالب المعتمد والفرصة الأصلية.</summary>
public sealed record TelegramComposeDto(long OpportunityId, string Title, string Text, string Link,
    long? TemplateVersionId, IReadOnlyList<TelegramComposeDestinationDto> Destinations);
