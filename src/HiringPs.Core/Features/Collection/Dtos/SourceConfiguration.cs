// اكتشاف روابط الفرص وجلب صفحاتها ومرفقاتها، مع فحص النطاقات وتسجيل التشخيص.
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>DTO لإعدادات موصل المصدر كما يقرأها مسار الجمع.</summary>
public class SourceConfiguration
{
    public string[] AllowedHosts { get; set; } = [];
    public string ListingLinkSelector { get; set; } = "";
    public string ContentSelector { get; set; } = "body";
    public string WaitSelector { get; set; } = "";
    public string NextSelector { get; set; } = "";
    public int MaxPages { get; set; } = 10;
    public string AppName { get; set; } = "";
    public int ForbiddenWaitSeconds { get; set; } = 20;
}
