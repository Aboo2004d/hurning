namespace HiringPs.Core;

/// <summary>عقد الاختلاف بين هوية LinkedIn الشخصية وهوية المؤسسة مع مشاركة بروتوكول الإرسال.</summary>
public interface ILinkedInDestination
{
    /// <summary>مفتاح الوجهة المحفوظ في سجلات الربط والنشر.</summary>
    string Key { get; }
    /// <summary>صلاحية OAuth اللازمة للنشر باسم هذه الوجهة.</summary>
    string PublishingScope { get; }
    /// <summary>بادئة URN التي يتطلبها Posts API لكاتب المنشور.</summary>
    string AuthorPrefix { get; }
    /// <summary>يتحقق من هوية الوجهة وصلاحية إدارتها دون نشر محتوى.</summary>
    Task<SocialIdentityDto> ResolveAsync(string expectedId, string token, CancellationToken ct);
}
