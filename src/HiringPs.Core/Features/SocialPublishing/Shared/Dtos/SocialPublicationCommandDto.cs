namespace HiringPs.Core;

/// <summary>طلب نشر مع هوية المحاولة والفاعل؛ لا يقبل هوية الحساب من المتصفح.</summary>
public sealed record SocialPublicationCommandDto(Guid Id, long OpportunityId, string Provider,
    long? TemplateVersionId, string Text, bool ConfirmRepeat, string Actor);
