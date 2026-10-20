using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>عميل HTTP لـLahza؛ يقيد المهلة والحجم ويفسر غلاف JSON دون الوصول إلى القاعدة أو منح اشتراك.</summary>
public sealed class LahzaApiClient(HttpClient http)
{
    /// <summary>يرسل طلبًا واحدًا إلى نطاق المزود الثابت دون إعادة تلقائية أو كشف بيانات الاعتماد في الأخطاء.</summary>
    public async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string credential,
        bool ready,
        string configurationStatus,
        CancellationToken ct
    )
    {
        if (!ready)
            throw new PaymentProviderException(configurationStatus);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var operation = timeout.Token;
        int? httpStatus = null;
        try
        {
            using var request = new HttpRequestMessage(method, "https://api.lahza.io/" + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            if (body != null)
                request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                operation
            );
            httpStatus = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                throw new PaymentProviderException("HttpFailure", httpStatus);
            if (response.Content.Headers.ContentLength > 262144)
                throw new PaymentProviderException("ResponseTooLarge", httpStatus);
            await using var stream = await response.Content.ReadAsStreamAsync(operation);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, operation)) > 0)
            {
                if (bounded.Length + count > 262144)
                    throw new PaymentProviderException("ResponseTooLarge", httpStatus);
                bounded.Write(buffer, 0, count);
            }
            var json = JsonDocument.Parse(bounded.ToArray());
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("status", out var status)
                || status.ValueKind != JsonValueKind.True
            )
            {
                json.Dispose();
                throw new PaymentProviderException("ApiRejected", httpStatus);
            }
            return json;
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            // Provider bodies and authorization headers must never reach logs or the browser.
            throw new PaymentProviderException(
                ex switch
                {
                    OperationCanceledException => "Timeout",
                    HttpRequestException => "NetworkFailure",
                    IOException => "ResponseReadFailure",
                    JsonException => "MalformedJson",
                    _ => "UnexpectedPayload",
                },
                httpStatus
            );
        }
    }
}
