using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiringPs.Core;

/// <summary>قواعد رقم المحفظة وOTP وحدود المبلغ ومرجع الطلب المشتركة بين التطبيق والمحول.</summary>
public static class JawwalPayRules
{
    /// <summary>ينشئ مرجع طلب المحفظة من معرّف الطلب دون بيانات شخصية.</summary>
    public static string ExternalReference(Guid orderId) => "hiring-" + orderId.ToString("N");

    /// <summary>يتحقق من أن المبلغ يقع ضمن حدود البوابة ودقة الكسور المدعومة.</summary>
    public static bool SupportsAmount(decimal amount) =>
        amount is >= 1 and <= 5000 && decimal.Round(amount, 2) == amount;

    /// <summary>يتحقق من صيغة رقم المحفظة المطلوبة ويرفض الأرقام غير المدعومة.</summary>
    public static string Phone(string? value)
    {
        var phone = value?.Trim() ?? "";
        if (
            phone.Length != 10
            || !phone.StartsWith("05", StringComparison.Ordinal)
            || !phone.All(char.IsAsciiDigit)
        )
            throw new InvalidOperationException("WalletMobileInvalid");
        return phone;
    }

    /// <summary>يتحقق من صيغة رمز OTP دون تسجيل الرمز أو افتراض صحته لدى المزود.</summary>
    public static bool ValidOtp(string? value) =>
        value is { Length: >= 1 and <= 32 } && value.All(char.IsAsciiDigit);
}
