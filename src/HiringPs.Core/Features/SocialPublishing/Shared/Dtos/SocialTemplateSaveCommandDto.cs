namespace HiringPs.Core;

/// <summary>طلب اعتماد نسخة قالب مع رقم النسخة النشطة المتوقع لحماية التزامن.</summary>
public sealed record SocialTemplateSaveCommandDto(string Provider, string Scope, long ExpectedActiveId,
    string Body, string Actor);
