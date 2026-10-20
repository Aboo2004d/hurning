using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يفصل تحديث هوية العضو وإبطال تحدياته وإنشاء رابط الاستعادة عن HTTP.</summary>
public sealed class MemberRecoveryService(HiringDb db, MembershipService membership, MemberVerification verification)
{
    /// <summary>يفصل ربط تليجرام القديم ويعيد طلب التحقق؛ يلزم تفويض الإدارة والتحقق من الهوية في المستدعي.</summary>
    public async Task<MemberRecoveryResultDto> PrepareAsync(string id, string mobile, string reason, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return new(MemberRecoveryOutcome.InvalidRequest);
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == id, ct);
        if (member is null)
            return new(MemberRecoveryOutcome.NotFound);
        member.Mobile = MembershipRules.Mobile(mobile);
        member.TelegramUserId = null;
        member.TelegramChatId = null;
        member.Activated = false;
        await db
            .MemberChallenges.Where(x => x.UserId == id && x.ConsumedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ConsumedAt, DateTimeOffset.UtcNow), ct);
        membership.Audit(
            actor,
            "Member.Recover",
            $"Member {id}; identity verified by administrator; {reason}"
        );
        await db.SaveChangesAsync(ct);
        try
        {
            var request = await verification.CreateAsync(id, ChallengePurpose.Registration, null, ct);
            return new(MemberRecoveryOutcome.Ready, request);
        }
        catch (InvalidOperationException ex)
        {
            return new(MemberRecoveryOutcome.ChallengeUnavailable, Notice: ex.Message);
        }
    }
}
