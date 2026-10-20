using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>ينفذ HTTP المشترك لمزودي النشر دون قراءة القاعدة أو تغيير حالات محاولات النشر.</summary>
public sealed class SocialProviderTransport(IConfiguration config, IHttpClientFactory clients, ILogger<SocialPublishing>? logger = null)
{
    internal ILogger<SocialPublishing>? Logger => logger;
    /// <summary>ينشئ عميل HTTP المسجل باسم SocialProviders بمهلته وسياسة التوجيه الحالية.</summary>
    public HttpClient Client() => clients.CreateClient("SocialProviders");
    public string FacebookRoot => "https://graph.facebook.com/" + Required("Integrations:Facebook:ApiVersion");
    /// <summary>يقرأ إعدادًا إلزاميًا ويرفض القيم الافتراضية أو الغائبة.</summary>
    public string Required(string key) => !string.IsNullOrWhiteSpace(config[key]) && config[key] != "***" ? config[key]! : throw new InvalidOperationException("IntegrationNotConfigured");
    /// <summary>يقرأ إعداد LinkedIn الإلزامي ويرفض القيم الفارغة أو الافتراضية.</summary>
    public string RequiredLinkedIn(string provider, string name)
    {
        var value = SocialPublishing.LinkedInSetting(config, provider, name);
        return !string.IsNullOrWhiteSpace(value) && value != "***" ? value
            : throw new InvalidOperationException("IntegrationNotConfigured");
    }
    /// <summary>يقرأ بيانات المورد ويتحقق من استجابته أو حدوده وفق عقد الخدمة.</summary>
    public async Task<JsonDocument> ReadAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await Client().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger?.LogWarning("SocialConnection Stage ProviderRequest Path {Path} HttpStatus {HttpStatus}",
                request.RequestUri?.AbsolutePath, (int)response.StatusCode);
            throw new InvalidOperationException("IntegrationProviderRejected");
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    /// <summary>ينشئ طلبًا موثقًا ويضيف إصدار API إلى نطاق LinkedIn REST عند الحاجة.</summary>
    public HttpRequestMessage Request(string url, string token, string provider = "LinkedIn")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (url.StartsWith("https://api.linkedin.com/rest/"))
        {
            request.Headers.Add("LinkedIn-Version", RequiredLinkedIn(provider, "ApiVersion"));
            request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
        }
        return request;
    }

    /// <summary>يرسل محاولة واحدة ويقرأ رؤوس الاستجابة؛ لا يعيد أمر النشر تلقائيًا.</summary>
    public async Task<HttpResponseMessage> SendPublicationAsync(HttpRequestMessage request, SocialPublishRequestDto publication, CancellationToken ct)
    {
        var response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        logger?.LogInformation("SocialPublish Attempt {AttemptId} Provider {Provider} Stage ResponseHeaders HttpStatus {HttpStatus}",
            publication.AttemptId, publication.Provider, (int)response.StatusCode);
        return response;
    }
    /// <summary>يميز الرفض الصريح عن استجابة قد يكون المزود نفذ معها النشر بالفعل.</summary>
    public static SocialPublishResultDto Rejected(HttpResponseMessage response) =>
        new((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ? SocialPublicationOutcome.Unknown : SocialPublicationOutcome.Failed);
}
