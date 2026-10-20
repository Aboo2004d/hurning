namespace HiringPs.Core;

/// <summary>نتيجة مزود النشر؛ لا تدعي النجاح عند غياب معرّف المنشور.</summary>
/// <param name="Status">رمز الحالة المتوافق مع سجل النشر الحالي.</param>
/// <param name="RemoteId">المعرّف الذي أعاده المزود، إن توفر.</param>
/// <param name="Url">رابط المنشور الذي أكد المزود إنشاءه.</param>
public sealed record SocialPublishResultDto(SocialPublicationOutcome Status, string RemoteId = "", string Url = "");
