using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

// A separate profile leaves the existing administrator Identity records untouched.
public sealed class MemberProfile
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(160)]
    public string Name { get; set; } = "";

    [MaxLength(32)]
    public string Mobile { get; set; } = "";

    [MaxLength(2)]
    public string Language { get; set; } = "ar";
    public long? TelegramUserId { get; set; }
    public long? TelegramChatId { get; set; }
    public bool Activated { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool NotificationsEnabled { get; set; } = true;
    public bool SavedRemindersEnabled { get; set; } = true;
    public DateTimeOffset NotificationsSince { get; set; } = DateTimeOffset.UtcNow;
    public bool Suspended { get; set; }

    [MaxLength(2000)]
    public string SuspensionReason { get; set; } = "";
    public DateTimeOffset? SuspendedAt { get; set; }
    public bool FreezeSubscription { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public sealed class MemberPreference
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = "";
    public int Kinds { get; set; } = 15;

    public string CategoriesJson { get; set; } = "[]";

    public string LocationsJson { get; set; } = "[]";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SavedOpportunity
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OpportunityAccessRule
{
    [Key]
    public OpportunityKind Kind { get; set; }
    public bool PublicDetails { get; set; } = true;
}

public sealed class MembershipPlan
{
    public int Id { get; set; }
    public bool Visible { get; set; } = true;
    public int SortOrder { get; set; }
    public int? CurrentVersionId { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

// Purchased versions are immutable. Editing creates a new version.
public sealed class MembershipPlanVersion
{
    public int Id { get; set; }
    public int PlanId { get; set; }

    [MaxLength(160)]
    public string NameAr { get; set; } = "";

    [MaxLength(160)]
    public string NameEn { get; set; } = "";

    [MaxLength(2000)]
    public string DescriptionAr { get; set; } = "";

    [MaxLength(2000)]
    public string DescriptionEn { get; set; } = "";

    [MaxLength(80)]
    public string BadgeAr { get; set; } = "";

    [MaxLength(80)]
    public string BadgeEn { get; set; } = "";

    [Precision(18, 2)]
    public decimal PriceIls { get; set; }
    public int Days { get; set; } = 30;
    public int DetailKinds { get; set; }
    public int AlertKinds { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Name(string language) => language == "en" ? NameEn : NameAr;
}

public sealed class MemberSubscription
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";
    public int PlanVersionId { get; set; }
    public MembershipPlanVersion PlanVersion { get; set; } = null!;
    public Guid? OrderId { get; set; }

    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool Cancelled { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public sealed class DiscountCode
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = "";

    // A null plan means this discount is available for every subscription plan.
    public int? PlanId { get; set; }

    [Range(0, 100)]
    public int Percent { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaximumUses { get; set; }
    public bool Enabled { get; set; } = true;
}

public enum PaymentState
{
    AwaitingPayment,
    Pending,
    Failed,
    Cancelled,
    Paid,
    ManuallyApproved,
}

public sealed class PaymentOrder
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(450)]
    public string UserId { get; set; } = "";
    public int PlanVersionId { get; set; }
    public MembershipPlanVersion PlanVersion { get; set; } = null!;
    public int? DiscountCodeId { get; set; }
    public int DiscountPercent { get; set; }

    [Precision(18, 2)]
    public decimal AmountIls { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "ILS";

    [MaxLength(30)]
    public string Gateway { get; set; } = "";

    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";

    [MaxLength(200)]
    public string? ProviderReference { get; set; }

    [MaxLength(2048)]
    public string? CheckoutUrl { get; set; }
    public DateTimeOffset? CheckoutStartedAt { get; set; }
    public PaymentState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }

    [MaxLength(100)]
    public string TermsVersion { get; set; } = "2026-10-18-no-refunds";
    public DateTimeOffset TermsAcceptedAt { get; set; }

    [MaxLength(2000)]
    public string DecisionReason { get; set; } = "";

    [MaxLength(2000)]
    public string PaymentEvidence { get; set; } = "";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public sealed class PaymentReview
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(2000)]
    public string Note { get; set; } = "";

    [MaxLength(100)]
    public string? AttachmentName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    [MaxLength(2000)]
    public string Resolution { get; set; } = "";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public enum ChallengePurpose
{
    Registration,
    PasswordReset,
    EnableNotifications,
    ChangeTelegram,
    AuthorizeTelegramChange,
}

public sealed class MemberChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(450)]
    public string UserId { get; set; } = "";
    public ChallengePurpose Purpose { get; set; }

    [MaxLength(64)]
    public string LinkHash { get; set; } = "";

    [MaxLength(1000)]
    public string CodeHash { get; set; } = "";

    [MaxLength(32)]
    public string ExpectedMobile { get; set; } = "";
    public long? TelegramUserId { get; set; }
    public long? ChatId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CodeSentAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Attempts { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public enum MemberMessageKind
{
    Opportunity,
    SavedDeadline,
    Payment,
    Subscription,
    Verification,
    Review,
}

public sealed class MemberMessage
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(200)]
    public string EventKey { get; set; } = "";
    public MemberMessageKind Kind { get; set; }
    public long? OpportunityId { get; set; }
    public DateTimeOffset? EventAt { get; set; }

    [MaxLength(4000)]
    public string Text { get; set; } = "";
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int Attempts { get; set; }

    [MaxLength(200)]
    public string LastError { get; set; } = "";

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

// A publication marker is written in the same transaction as publication.
public sealed class OpportunityPublication
{
    public Opportunity Opportunity { get; set; } = null!;

    [Key]
    public long OpportunityId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? AfterUserId { get; set; }
    public bool Completed { get; set; }
}

public sealed class TelegramInbox
{
    [Key]
    public long Id { get; set; }

    [MaxLength(32000)]
    public string Json { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int Attempts { get; set; }
}

public sealed class MembershipRuntimeState
{
    [Key]
    public int Id { get; set; } = 1;
    public DateTimeOffset? LockedUntil { get; set; }
    public long ReminderAfterSubscription { get; set; }
    public long ReminderAfterSaved { get; set; }
}

public sealed class PaymentGatewaySetting
{
    [Key, MaxLength(30)]
    public string Key { get; set; } = "";
    public bool Enabled { get; set; }
}

// A single session per order. ConfirmationStartedAt is a permanent, durable claim:
// the partner explicitly forbids a second confirm call, even after an error.
public sealed class WalletPaymentSession
{
    [Key]
    public Guid OrderId { get; set; }
    public PaymentOrder Order { get; set; } = null!;

    [MaxLength(12)]
    public string GatewayEnvironment { get; set; } = "Production";

    [MaxLength(10)]
    public string Phone { get; set; } = "";

    [MaxLength(4096)]
    public string? ProtectedSessionKey { get; set; }

    [MaxLength(64)]
    public string? SessionKeyHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool InitiationRejected { get; set; }
    public DateTimeOffset? ConfirmationStartedAt { get; set; }
    public bool ConfirmationRejected { get; set; }
    public long? TransactionId { get; set; }

    [MaxLength(200)]
    public string? JawwalReference { get; set; }

    [Precision(18, 2)]
    public decimal? ConfirmedAmount { get; set; }

    [Precision(18, 2)]
    public decimal? JawwalFee { get; set; }

    [Precision(18, 2)]
    public decimal? PlatformFee { get; set; }

    [Precision(18, 2)]
    public decimal? NetCredited { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}
