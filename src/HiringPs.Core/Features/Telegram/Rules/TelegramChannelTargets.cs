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

/// <summary>مكون TelegramChannelTargets ضمن مجال Telegram؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class TelegramChannelTargets
{
    public const string Provider = "Telegram";
    public static readonly string[] All = ["Jobs", "Training", "Tenders", "All"];

    /// <summary>يتحقق من أن رمز الوجهة ضمن قنوات التوزيع المعتمدة.</summary>
    public static bool IsTarget(string? target) => target is not null && All.Contains(target);

    /// <summary>يعيد قناة نوع الفرصة ثم القناة العامة دون إتاحة قنوات الأنواع الأخرى.</summary>
    public static string[] ForOpportunity(OpportunityKind kind) => kind switch
    {
        OpportunityKind.Job => ["Jobs", "All"],
        OpportunityKind.Training => ["Training", "All"],
        OpportunityKind.Tender or OpportunityKind.ProcurementNotice => ["Tenders", "All"],
        _ => [],
    };

    /// <summary>يعيد أنواع الفرص الخاصة بوجهة القناة.</summary>
    public static OpportunityKind[] Kinds(string target) =>
        target switch
        {
            "Jobs" => [OpportunityKind.Job],
            "Training" => [OpportunityKind.Training],
            "Tenders" => [OpportunityKind.Tender, OpportunityKind.ProcurementNotice],
            "All" => Enum.GetValues<OpportunityKind>(),
            _ => [],
        };

    /// <summary>يتحقق من صيغة عنوان القناة المسموح بها.</summary>
    public static bool ValidChannel(string? channel) =>
        channel is not null
        && Regex.IsMatch(channel, "^(@[A-Za-z][A-Za-z0-9_]{3,31}|-?[0-9]{5,20})$", RegexOptions.CultureInvariant);

    /// <summary>Configured chat for a target ("@channel" or numeric id), or "" when missing/invalid.</summary>
    public static string Channel(IConfiguration configuration, string target)
    {
        var value = configuration["Telegram:Channels:" + target]?.Trim() ?? "";
        return ValidChannel(value) ? value : "";
    }
}
