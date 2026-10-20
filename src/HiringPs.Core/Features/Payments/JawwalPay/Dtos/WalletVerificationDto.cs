namespace HiringPs.Core;

/// <summary>حقائق جلسة المحفظة اللازمة للتحقق؛ لا تتضمن رمز الجلسة المشفر أو OTP.</summary>
public sealed record WalletVerificationDto(
    string Phone, long? TransactionId, decimal? ConfirmedAmount,
    DateTimeOffset? ConfirmationStartedAt, bool ConfirmationRejected);
