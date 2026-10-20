namespace HiringPs.Core;

/// <summary>نتيجة الاستعادة؛ سر التحدي مخصص للرابط ولا يسجل في التدقيق.</summary>
public sealed record MemberRecoveryResultDto(MemberRecoveryOutcome Outcome,
    VerificationRequest? Request = null, string? Notice = null);
