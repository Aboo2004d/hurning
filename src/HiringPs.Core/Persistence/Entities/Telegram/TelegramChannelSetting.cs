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

/// <summary>سجل TelegramChannelSetting وعلاقاته؛ يطابق مخطط تحديث المنصة المحفوظ.</summary>
public sealed class TelegramChannelSetting
{
    /// <summary>رمز وجهة القناة Jobs أو Training أو Tenders أو All.</summary>
    [Key, MaxLength(30)]
    public string Target { get; set; } = "";
    /// <summary>يسمح بتشغيل الوجهة دون حذف سجل النشر السابق.</summary>
    public bool Enabled { get; set; }
    /// <summary>بداية تفعيل الوجهة؛ تمنع نشر الفرص السابقة بأثر رجعي.</summary>
    public DateTimeOffset? EnabledAt { get; set; }

    /// <summary>هوية الفاعل المسجلة مع إجراء الإدارة.</summary>
    [MaxLength(256)]
    public string Actor { get; set; } = "";
}
