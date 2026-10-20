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

/// <summary>مكون TelegramChannelResult ضمن مجال Telegram؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record TelegramChannelResult(
    TelegramChannelOutcome Outcome,
    string Code,
    int? RetryAfterSeconds = null,
    string RemoteId = "",
    string Url = ""
);
