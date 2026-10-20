using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>يحفظ تصنيف فشل إرسال Telegram دون تضمين رمز البوت أو حمولة الرسالة.</summary>
public sealed class TelegramDeliveryException(int status)
    : Exception("Telegram delivery failed (" + status + ").")
{
    public int Status { get; } = status;
}
