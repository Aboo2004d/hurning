// تنزيل PDF ضمن الحدود المسموحة ثم قراءة الصفحات وحفظ النصوص بواسطة مسار النص وOCR.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HiringPs.Core;

/// <summary>مكون AttachmentReader ضمن مجال Pdf؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public class AttachmentReader(UrlPolicy policy, IConfiguration? configuration = null)
{
    public const int MaxBytes = 8 * 1024 * 1024;

    /// <summary>ينفذ ReadPdfAsync وفق قواعد AttachmentReader الحالية دون تغيير عقد المستدعين.</summary>
    public async Task<string> ReadPdfAsync(string url, string[] hosts, CancellationToken ct)
    {
        using var downloadBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        downloadBudget.CancelAfter(TimeSpan.FromSeconds(40));
        byte[] bytes;
        try
        {
            bytes = await DownloadAsync(url, hosts, downloadBudget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ContentRetryableException(
                "PDF download timed out after 40 seconds; retry scheduled."
            );
        }
        return (
            await new PdfTextReader(configuration ?? new ConfigurationBuilder().Build()).ReadAsync(
                bytes,
                ct
            )
        ).Text;
    }

    /// <summary>ينفذ DownloadAsync وفق قواعد AttachmentReader الحالية دون تغيير عقد المستدعين.</summary>
    private async Task<byte[]> DownloadAsync(string url, string[] hosts, CancellationToken ct)
    {
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ConnectCallback = async (ctx, cancel) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, cancel);
            if (addresses.Length == 0 || addresses.Any(UrlPolicy.IsPrivate))
                throw new InvalidOperationException("Attachment network target is not public.");
            var target = addresses
                .OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .First();
            var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(target, ctx.DnsEndPoint.Port), cancel);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        var current = url;
        for (var redirects = 0; redirects < 5; redirects++)
        {
            var uri = await policy.ValidateAsync(current, hosts, ct);
            using var response = await http.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            if (
                (int)response.StatusCode is >= 300 and < 400
                && response.Headers.Location is { } location
            )
            {
                current = new Uri(uri, location).ToString();
                continue;
            }

            if (IsTemporary(response.StatusCode))
                throw new ContentRetryableException(
                    $"Attachment HTTP {(int)response.StatusCode}: temporary source error; retry scheduled."
                );
            if (!response.IsSuccessStatusCode)
                throw new ContentNeedsReviewException(
                    "Attachment unavailable; review before publishing."
                );
            if (response.Content.Headers.ContentLength > MaxBytes)
                throw new ContentNeedsReviewException("Attachment exceeds 8 MB limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                    throw new ContentNeedsReviewException("Attachment exceeds 8 MB limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }

            return buffer.ToArray();
        }

        throw new ContentNeedsReviewException("Attachment redirect limit reached.");
    }

    /// <summary>ينفذ IsTemporary وفق قواعد AttachmentReader الحالية دون تغيير عقد المستدعين.</summary>
    public static bool IsTemporary(HttpStatusCode status) =>
        status
            is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;

    /// <summary>ينفذ ExtractText وفق قواعد AttachmentReader الحالية دون تغيير عقد المستدعين.</summary>
    public static string ExtractText(byte[] bytes)
    {
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
            throw new ContentNeedsReviewException("Attachment is not a readable PDF.");
        try
        {
            using var document = PdfDocument.Open(bytes);
            if (document.NumberOfPages > 40)
                throw new ContentNeedsReviewException("PDF exceeds 40 pages; review needed.");
            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            }

            if (text.ToString().Count(char.IsLetter) < 40)
                throw new ContentNeedsReviewException(
                    "PDF is image-only or unreadable; OCR/manual review required."
                );
            return text.ToString();
        }
        catch (ContentNeedsReviewException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ContentNeedsReviewException("PDF parsing failed; manual review required.");
        }
    }
}
