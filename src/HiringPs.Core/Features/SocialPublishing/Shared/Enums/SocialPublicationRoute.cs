namespace HiringPs.Core;

/// <summary>الوجهة المنطقية التالية بعد محاولة النشر دون الاعتماد على MVC.</summary>
public enum SocialPublicationRoute
{
    Compose = 0,
    InvalidRequest = 1,
    Integrations = 2,
}
