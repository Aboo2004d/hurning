// النشر التلقائي في قناة تليجرام العامة: الإعدادات، العميل، وطابور التسليم فوق سجل النشر الاجتماعي.
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون TelegramChannelOutcome ضمن مجال Telegram؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public enum TelegramChannelOutcome
{
    Sent = 0,
    RetryLater = 1,
    Rejected = 2,
    Ambiguous = 3,
}

// Uses the existing bot token only. Never logs or throws the request URI: it contains the token.
