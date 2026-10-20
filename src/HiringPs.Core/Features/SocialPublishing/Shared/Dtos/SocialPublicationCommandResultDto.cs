namespace HiringPs.Core;

/// <summary>نتيجة أمر النشر ورمز الرسالة المترجم؛ لا تتضمن رد المزود أو رموزه السرية.</summary>
public sealed record SocialPublicationCommandResultDto(SocialPublicationRoute Route, string? Notice = null);
