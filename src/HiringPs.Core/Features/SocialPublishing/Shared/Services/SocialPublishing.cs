using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>ينسق الربط وفك الرموز وحفظ نتيجة النشر؛ يفوض عقود HTTP لعملاء المزودين المنفصلين.</summary>
public sealed class SocialPublishing
{
    private readonly HiringDb db;
    private readonly IConfiguration config;
    private readonly IDataProtector protector;
    private readonly SocialProviderTransport transport;
    private readonly FacebookPublishingClient facebook;
    private readonly LinkedInPublishingClient linkedin;

    /// <summary>ينشئ خدمة التنسيق؛ تقبل العملاء من DI وتحتفظ بتوافق الإنشاء المباشر الحالي.</summary>
    public SocialPublishing(HiringDb db, IConfiguration config, IDataProtectionProvider protection,
        IHttpClientFactory clients, ILogger<SocialPublishing>? logger = null,
        SocialProviderTransport? transport = null, FacebookPublishingClient? facebook = null,
        LinkedInPublishingClient? linkedin = null)
    {
        this.db = db;
        this.config = config;
        protector = protection.CreateProtector("HiringPs.SocialToken.v1");
        this.transport = transport ?? new SocialProviderTransport(config, clients, logger);
        this.facebook = facebook ?? new FacebookPublishingClient(this.transport);
        this.linkedin = linkedin ?? new LinkedInPublishingClient(this.transport);
    }
    /// <summary>يتحقق من أن وجهة الربط ضمن مزودي النشر المدعومين.</summary>
    public static bool IsProvider(string provider) => provider is "Facebook" or "LinkedIn" or "LinkedInPersonal";
    /// <summary>يميز وجهتي LinkedIn عن مزودي النشر الآخرين.</summary>
    public static bool IsLinkedIn(string provider) => provider is "LinkedIn" or "LinkedInPersonal";
    /// <summary>يحدد مجموعة القوالب المشتركة للوجهة دون تغيير سجل ربطها.</summary>
    public static string TemplateProvider(string provider) => provider == "LinkedInPersonal" ? "LinkedIn" : provider;
    /// <summary>يقرأ إعداد الوجهة الشخصية مع الرجوع لإعداد LinkedIn المشترك عند عدم تعريفه.</summary>
    public static string LinkedInSetting(IConfiguration config, string provider, string name) =>
        config[$"Integrations:{provider}:{name}"] ?? config[$"Integrations:LinkedIn:{name}"] ?? "";
    /// <summary>يشفر رمز الوصول بمفاتيح التطبيق قبل تخزينه في القاعدة.</summary>
    public string Protect(string token) => protector.Protect(token);
    /// <summary>يتحقق من أن الفرصة منشورة وغير مغلقة ولم ينته موعدها قبل إتاحة النشر الخارجي.</summary>
    public static bool Eligible(Opportunity item, DateOnly today) =>
        item.Status == PublicationStatus.Published && (item.Deadline is null || item.Deadline >= today) && string.IsNullOrWhiteSpace(item.ClosureEvidence);
    /// <summary>ينشئ رابط الفرصة من الأصل العام HTTPS واللغة المعتمدين.</summary>
    public string OpportunityUrl(long id, string language)
    {
        var origin = config["Seo:PublicOrigin"]?.TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("PublicOriginRequired");
        return origin + (language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "/en" : "/ar") + "/Opportunities/Details/" + id;
    }

    /// <summary>يحل هوية الصفحة ورمزها عبر العميل الخاص بالمزود بعد التحقق من المدخلات.</summary>
    public Task<(string Token, string Name)> ResolvePageAsync(string provider, string pageId, string token, CancellationToken ct)
    {
        if (!IsProvider(provider) || !System.Text.RegularExpressions.Regex.IsMatch(pageId, "^[0-9]{1,40}$")) throw new InvalidOperationException("IntegrationPageInvalid");
        return provider == "Facebook" ? facebook.ResolvePageAsync(pageId, token, ct) : linkedin.ResolvePageAsync(pageId, token, ct);
    }
    /// <summary>يقرأ هوية الحساب الشخصي من LinkedIn دون إنشاء حساب عضو أو منشور.</summary>
    public Task<(string Id, string Name)> ResolvePersonalAsync(string token, CancellationToken ct) => linkedin.ResolvePersonalAsync(token, ct);
    /// <summary>يتحقق من صلاحية رمز LinkedIn وصلاحية النشر الخاصة بوجهته.</summary>
    public Task ValidateLinkedInGrantAsync(string provider, string token, CancellationToken ct) => linkedin.ValidateLinkedInGrantAsync(provider, token, ct);
    /// <summary>يفحص الاتصال أو الهوية والصلاحيات دون تنفيذ دفعة أو إنشاء منشور.</summary>
    public async Task<ToolProbe> ProbeAsync(string provider, CancellationToken ct)
    {
        if (provider == "GoogleLogin")
        {
            if (string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"]) || config["Authentication:Google:ClientId"] == "***" || string.IsNullOrWhiteSpace(config["Authentication:Google:ClientSecret"]) || config["Authentication:Google:ClientSecret"] == "***")
                return new(provider, ToolState.NotConfigured, "IntegrationNotConfigured");
            if (await db.SocialConnections.AnyAsync(x => x.Provider == "Google" && !x.Enabled, ct))
                return new(provider, ToolState.Disabled, "IntegrationDisabled");
            try
            {
                var callback = OpportunityUrl(0, "ar").Split("/ar/")[0] + "/signin-google";
                using var response = await transport.Client().GetAsync("https://accounts.google.com/.well-known/openid-configuration", ct);
                return new(provider, response.IsSuccessStatusCode ? ToolState.Configured : ToolState.Problem, "GoogleConfigurationOnly", callback);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException) { return new(provider, ToolState.Problem, "IntegrationUnavailable"); }
        }
        var connection = await db.SocialConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Provider == provider, ct);
        if (connection is null) return new(provider, ToolState.NotConfigured, "IntegrationNotConfigured");
        try
        {
            if (!connection.Enabled) return new(provider, ToolState.Disabled, "IntegrationDisabled");
            if (connection.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("IntegrationExpired");
            var token = protector.Unprotect(connection.ProtectedToken);
            if (provider == "Facebook") await facebook.ProbeAsync(connection.PageId, token, ct);
            else await linkedin.ProbeAsync(provider, connection.PageId, token, ct);
            return new(provider, ToolState.Ready, provider == "LinkedInPersonal" ? "IntegrationPersonalReady" : "IntegrationPageReady", connection.PageName + " · " + connection.PageId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or JsonException or KeyNotFoundException or System.Security.Cryptography.CryptographicException)
        { return new(provider, ToolState.Problem, ex is InvalidOperationException ? ex.Message : "IntegrationUnavailable", connection.PageName + " · " + connection.PageId); }
    }

    /// <summary>يتحقق من الربط المحفوظ ويرسل المحاولة المحجوزة مرة واحدة، ثم يحدّث نتيجتها في الذاكرة ليحفظها المستدعي.</summary>
    public async Task PublishAsync(SocialPublication attempt, CancellationToken ct)
    {
        var connection = await db.SocialConnections.AsNoTracking().SingleAsync(x => x.Provider == attempt.Provider, ct);
        if (!connection.Enabled) throw new InvalidOperationException("IntegrationDisabled");
        if (connection.PageId != attempt.PageId) throw new InvalidOperationException("IntegrationPageInvalid");
        if (connection.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("IntegrationExpired");
        var token = protector.Unprotect(connection.ProtectedToken);
        var publication = new SocialPublishRequestDto(attempt.Id, attempt.Provider, connection.PageId, attempt.Text);
        var result = attempt.Provider == "Facebook"
            ? await facebook.PublishAsync(publication, token, ct)
            : await linkedin.PublishAsync(publication, token, ct);
        attempt.Status = result.Status.ToString();
        attempt.RemoteId = result.RemoteId;
        attempt.Url = result.Url;
    }
}
