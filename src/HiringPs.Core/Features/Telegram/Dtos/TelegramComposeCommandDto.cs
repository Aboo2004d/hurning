namespace HiringPs.Core;

/// <summary>أمر إدارة لإضافة رسالة قناة إلى طابور التسليم؛ لا يستقبل معرّفات قنوات حرة.</summary>
public sealed record TelegramComposeCommandDto(Guid Id, long OpportunityId, long? TemplateVersionId,
    string Text, string[] Targets, string Actor);
