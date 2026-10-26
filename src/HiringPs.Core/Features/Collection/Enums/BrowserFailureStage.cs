namespace HiringPs.Core;

/// <summary>المرحلة الفعلية لفشل المتصفح، قبل محرك الاتصال أو أثناء إطلاقه أو استخدامه.</summary>
public enum BrowserFailureStage
{
    DriverStartup,
    DriverConnection,
    CustomBrowserLaunch,
    BrowserLaunch,
    BrowserVerification,
}
