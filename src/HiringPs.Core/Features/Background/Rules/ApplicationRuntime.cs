using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يفصل الدور التشغيلي عن بيئة الدفع وعن تفعيل موصل Telegram.</summary>
public static class ApplicationRuntime
{
    /// <summary>يقرأ الدور الصريح، ويختار السيرفر افتراضيًا ويرفض القيم غير المعروفة.</summary>
    public static ApplicationRuntimeMode Mode(IConfiguration config)
    {
        var value = config["Runtime:Mode"] ?? "Server";
        return Enum.TryParse<ApplicationRuntimeMode>(value, true, out var mode) && Enum.IsDefined(mode)
            ? mode : throw new InvalidOperationException("Runtime:Mode must be Server or Local.");
    }
}
