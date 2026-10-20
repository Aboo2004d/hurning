namespace HiringPs.Core;

/// <summary>هوية وجهة نشر تم التحقق منها لدى المزود دون الاعتماد على معرّف يرسله المتصفح.</summary>
/// <param name="Id">المعرّف الثابت للحساب أو الصفحة لدى المزود.</param>
/// <param name="Name">اسم الحساب أو الصفحة الذي يعرض للإدارة.</param>
public sealed record SocialIdentityDto(string Id, string Name);
