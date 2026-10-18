using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public sealed record VerificationRequest(Guid Id, string Token, ChallengePurpose Purpose);

public sealed class MemberVerification(
    HiringDb db,
    TelegramClient telegram,
    MembershipService membership
)
{
    private static readonly PasswordHasher<MemberChallenge> Hasher = new();

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<VerificationRequest> CreateAsync(
        string userId,
        ChallengePurpose purpose,
        string? mobile,
        CancellationToken ct
    )
    {
        if (!telegram.Ready)
            throw new InvalidOperationException("TelegramUnavailable");
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        var member = await membership.LockMemberAsync(userId, ct);
        if (member.Suspended)
            throw new InvalidOperationException("AccountSuspended");
        var recent = await db.MemberChallenges.AnyAsync(
            x => x.UserId == userId && x.Purpose == purpose && x.CreatedAt > now.AddMinutes(-1),
            ct
        );
        if (recent)
            throw new InvalidOperationException("VerificationWait");
        await db
            .MemberChallenges.Where(x => x.UserId == userId && x.ConsumedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ConsumedAt, now), ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var challenge = new MemberChallenge
        {
            UserId = userId,
            Purpose = purpose,
            LinkHash = Hash(token),
            ExpectedMobile = mobile is null ? member.Mobile : MembershipRules.Mobile(mobile),
            ExpiresAt = now.AddMinutes(15),
        };
        db.MemberChallenges.Add(challenge);
        if (purpose is not (ChallengePurpose.Registration or ChallengePurpose.ChangeTelegram))
        {
            if (member.TelegramChatId is null)
                throw new InvalidOperationException("TelegramUnavailable");
            challenge.ChatId = member.TelegramChatId;
            challenge.TelegramUserId = member.TelegramUserId;
            IssueCode(challenge);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (challenge.ChatId is { } chat)
            await SendCodeAsync(challenge, chat, ct);
        return new(challenge.Id, token, purpose);
    }

    // The OTP itself is transient in memory, not persisted in messages or audits.
    private string? code;

    private void IssueCode(MemberChallenge challenge)
    {
        code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        challenge.CodeHash = Hasher.HashPassword(challenge, code);
        challenge.CodeSentAt = DateTimeOffset.UtcNow;
        challenge.ExpiresAt = challenge.CodeSentAt.Value.AddMinutes(5);
    }

    private async Task SendCodeAsync(MemberChallenge challenge, long chat, CancellationToken ct)
    {
        var language = await db
            .MemberProfiles.Where(x => x.UserId == challenge.UserId)
            .Select(x => x.Language)
            .SingleAsync(ct);
        var text =
            language == "en"
                ? "Verification code: " + code + "\nValid for 5 minutes. Do not share it."
                : "رمز التحقق: " + code + "\nصالح لخمس دقائق. لا تشاركه.";
        await telegram.SendAsync(chat, text, ct);
    }

    public async Task<bool> StartAsync(string token, long from, long chat, CancellationToken ct)
    {
        if (from != chat || token.Length != 48)
            return false;
        var hash = Hash(token);
        var now = DateTimeOffset.UtcNow;
        var challenge = await db.MemberChallenges.SingleOrDefaultAsync(
            x => x.LinkHash == hash && x.ConsumedAt == null && x.ExpiresAt > now,
            ct
        );
        if (
            challenge is null
            || challenge.Purpose
                is not (ChallengePurpose.Registration or ChallengePurpose.ChangeTelegram)
        )
            return false;
        if (challenge.TelegramUserId is not null && challenge.TelegramUserId != from)
            return false;
        if (
            await db.MemberProfiles.AnyAsync(
                x => x.TelegramUserId == from && x.UserId != challenge.UserId,
                ct
            )
        )
            return false;
        challenge.TelegramUserId = from;
        challenge.ChatId = chat;
        await db.SaveChangesAsync(ct);
        await telegram.SendAsync(
            chat,
            "اضغط مشاركة رقمي للتحقق من رقم التسجيل. / Share your own number to verify registration.",
            ct,
            true
        );
        return true;
    }

    public async Task<bool> ContactAsync(
        long from,
        long chat,
        long contactUserId,
        string mobile,
        CancellationToken ct
    )
    {
        if (from != chat || contactUserId != from)
            return false;
        var now = DateTimeOffset.UtcNow;
        var challenge = await db
            .MemberChallenges.Where(x =>
                x.TelegramUserId == from
                && x.ChatId == chat
                && x.ConsumedAt == null
                && x.ExpiresAt > now
            )
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (
            challenge is null
            || challenge.ExpectedMobile != MembershipRules.Mobile(mobile)
            || challenge.CodeSentAt > now.AddMinutes(-1)
        )
            return false;
        IssueCode(challenge);
        await db.SaveChangesAsync(ct);
        await SendCodeAsync(challenge, chat, ct);
        return true;
    }

    public async Task<bool> VerifyAsync(
        VerificationRequest request,
        string input,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var owner = await db
            .MemberChallenges.Where(x => x.Id == request.Id)
            .Select(x => x.UserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null)
            return false;
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        var member = await membership.LockMemberAsync(owner, ct);
        var challenge = await db.MemberChallenges.SingleAsync(x => x.Id == request.Id, ct);
        if (
            challenge.LinkHash != Hash(request.Token)
            || challenge.Purpose != request.Purpose
            || challenge.ConsumedAt != null
            || challenge.ExpiresAt <= now
            || challenge.Attempts >= 5
            || challenge.CodeHash.Length == 0
            || member.Suspended
        )
            return false;
        challenge.Attempts++;
        var valid =
            input.Length == 6
            && input.All(char.IsAsciiDigit)
            && Hasher.VerifyHashedPassword(challenge, challenge.CodeHash, input)
                != PasswordVerificationResult.Failed;
        if (valid)
        {
            if (
                challenge.Purpose
                is ChallengePurpose.Registration
                    or ChallengePurpose.ChangeTelegram
            )
            {
                if (challenge.TelegramUserId is null || challenge.ChatId is null)
                    return false;
                if (
                    await db.MemberProfiles.AnyAsync(
                        x => x.UserId != owner && x.TelegramUserId == challenge.TelegramUserId,
                        ct
                    )
                )
                    return false;
                member.TelegramUserId = challenge.TelegramUserId;
                member.TelegramChatId = challenge.ChatId;
                member.Mobile = challenge.ExpectedMobile;
                member.Activated = true;
            }
            if (challenge.Purpose == ChallengePurpose.EnableNotifications)
            {
                member.NotificationsEnabled = true;
                member.NotificationsSince = now;
            }
            challenge.ConsumedAt = now;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return valid;
    }
}
