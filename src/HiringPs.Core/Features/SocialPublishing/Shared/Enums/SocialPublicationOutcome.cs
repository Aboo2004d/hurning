namespace HiringPs.Core;

/// <summary>نتيجة إرسال واحدة لدى المزود؛ تتحول إلى الرمز النصي الحالي عند حفظ سجل النشر.</summary>
public enum SocialPublicationOutcome
{
    /// <summary>أكد المزود إنشاء المنشور وأعاد معرّفه.</summary>
    Published = 0,
    /// <summary>أعاد المزود رفضًا صريحًا لطلب النشر.</summary>
    Failed = 1,
    /// <summary>لا يمكن الجزم بالنتيجة؛ لا يُعاد الإرسال تلقائيًا لتجنب التكرار.</summary>
    Unknown = 2,
}
