namespace HiringPs.Core;

/// <summary>نتيجة إعداد استعادة عضو بعد التحقق الإداري من هويته.</summary>
public enum MemberRecoveryOutcome { Ready = 0, NotFound = 1, InvalidRequest = 2, ChallengeUnavailable = 3 }
