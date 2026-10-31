namespace HiringPs.Core;
/// <summary>إرسال وظيفة محجوزة عبر قوالب النشر ومسار الإرسال الموحد.</summary>
public interface IRandomPublicationDispatcher
{
    /// <summary>يرسل محاولة واحدة؛ النتيجة غير المحسومة لا يعاد إرسالها تلقائيًا.</summary>
    Task<RandomPublicationDeliveryDto> SendAsync(RandomPublicationRun run, CancellationToken ct);
}
