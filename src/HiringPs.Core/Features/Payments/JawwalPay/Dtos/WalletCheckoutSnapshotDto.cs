namespace HiringPs.Core;

/// <summary>حالة عرض المحفظة؛ لا تنقل مفتاح الجلسة أو بصمته أو رقم الهاتف الكامل إلى الواجهة.</summary>
public sealed record WalletCheckoutSnapshotDto(bool HasKey, DateTimeOffset ExpiresAt,
    DateTimeOffset? ConfirmationStartedAt, bool InitiationRejected, bool ConfirmationRejected, string PhoneHint);
