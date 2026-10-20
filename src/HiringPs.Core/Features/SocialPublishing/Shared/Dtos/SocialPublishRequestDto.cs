namespace HiringPs.Core;

/// <summary>بيانات إرسال منشور إلى مزود خارجي دون تمرير كيان EF أو رمز وصول داخل السجل.</summary>
/// <param name="AttemptId">معرّف محاولة النشر للتتبع ومنع تكرار الإرسال.</param>
/// <param name="Provider">وجهة النشر المحجوزة لهذه المحاولة.</param>
/// <param name="AuthorId">معرّف الصفحة أو الحساب المصرح له بالنشر.</param>
/// <param name="Text">نص المنشور النهائي متضمنًا الرابط المعتمد.</param>
public sealed record SocialPublishRequestDto(Guid AttemptId, string Provider, string AuthorId, string Text);
