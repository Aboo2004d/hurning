namespace HiringPs.Core;

/// <summary>إسقاط حقول الإيصال اللازمة للإدارة دون حالة جلسة المحفظة أو أسرارها.</summary>
public sealed record WalletReceiptSummary(
    long? TransactionId,
    string? JawwalReference,
    decimal? Amount,
    decimal? JawwalFee,
    decimal? PlatformFee,
    decimal? NetCredited
);
