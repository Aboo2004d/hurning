using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HiringPs.Core;

/// <summary>عميل HTTP لـJawwalPay؛ يقيد المهلة والحجم ويفسر غلاف JSON دون الوصول إلى القاعدة أو منح اشتراك.</summary>
public sealed class JawwalPayApiClient(HttpClient http)
{
    /// <summary>يرسل طلبًا واحدًا إلى نطاق المزود الثابت دون إعادة تلقائية أو كشف بيانات الاعتماد في الأخطاء.</summary>
    public async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string credential,
        bool ready,
        CancellationToken ct,
        bool allowRejection = false
    )
    {
        if (!ready)
            throw new PaymentProviderException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        int? httpStatus = null;
        try
        {
            using var request = new HttpRequestMessage(method, "https://store.portal.ps" + path);
            request.Headers.Add("X-Partner-Key", credential);
            if (body != null)
                request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token
            );
            httpStatus = (int)response.StatusCode;
            if (
                !response.IsSuccessStatusCode
                && !(allowRejection && response.StatusCode == HttpStatusCode.UnprocessableEntity)
            )
                throw new PaymentProviderException("WalletHttpFailure", httpStatus);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int size;
            while ((size = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (bounded.Length + size > 262144)
                    throw new PaymentProviderException("WalletResponseTooLarge", httpStatus);
                bounded.Write(buffer, 0, size);
            }
            var json = JsonDocument.Parse(bounded.ToArray());
            if (
                json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("ok", out var ok)
            )
            {
                json.Dispose();
                throw new PaymentProviderException("WalletMalformedEnvelope", httpStatus);
            }
            if (
                allowRejection
                && response.StatusCode == HttpStatusCode.UnprocessableEntity
                && ok.ValueKind == JsonValueKind.False
            )
            {
                json.Dispose();
                throw new WalletRequestRejectedException();
            }
            if (!response.IsSuccessStatusCode || ok.ValueKind != JsonValueKind.True)
            {
                json.Dispose();
                throw new PaymentProviderException("WalletApiRejected", httpStatus);
            }
            return json;
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or InvalidOperationException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            throw new PaymentProviderException(
                ex switch
                {
                    OperationCanceledException => "WalletTimeout",
                    HttpRequestException => "WalletNetworkFailure",
                    IOException => "WalletResponseReadFailure",
                    JsonException => "WalletMalformedJson",
                    _ => "WalletUnexpectedPayload",
                },
                httpStatus
            );
        }
    }
}
