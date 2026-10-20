namespace HiringPs.Core;

/// <summary>حد بيانات محدد لمحولات الدفع؛ يعزل استعلامات EF وحفظ الإيصال عن بروتوكول المزود.</summary>
public interface IPaymentGatewayData
{
    /// <summary>يقرأ بيانات مشتري موجود دون تمرير كيانات العضوية للمحول.</summary>
    Task<PaymentBuyerDto> BuyerAsync(string userId, CancellationToken ct);
    /// <summary>يقرأ حقائق تحقق المحفظة ضمن بيئة الطلب فقط.</summary>
    Task<WalletVerificationDto?> WalletAsync(Guid orderId, string environment, CancellationToken ct);
    /// <summary>يحفظ الإيصال مرة متوافقة مع معرّفه والبيئة ويرفض التعارض.</summary>
    Task SaveWalletReceiptAsync(Guid orderId, string environment, WalletReceipt receipt, CancellationToken ct);
}
