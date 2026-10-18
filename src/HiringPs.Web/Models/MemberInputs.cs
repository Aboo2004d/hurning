using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class RegisterInput
{
    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string Name { get; set; } = "";

    [
        Required(ErrorMessage = "FieldRequired"),
        EmailAddress(ErrorMessage = "EmailInvalid"),
        StringLength(256)
    ]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "FieldRequired"), StringLength(32)]
    public string Mobile { get; set; } = "";

    [
        Required(ErrorMessage = "FieldRequired"),
        MinLength(8, ErrorMessage = "PasswordMinLength"),
        DataType(DataType.Password)
    ]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "PasswordMismatch"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class VerificationInput
{
    [Required(ErrorMessage = "FieldRequired"), RegularExpression("^[0-9]{6}$")]
    public string Code { get; set; } = "";
    public string? BotLink { get; set; }
    public string? NewMobile { get; set; }
}

public sealed class ResetInput
{
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
    public string Email { get; set; } = "";
}

public sealed class ResetPasswordInput
{
    [
        Required(ErrorMessage = "FieldRequired"),
        MinLength(8, ErrorMessage = "PasswordMinLength"),
        DataType(DataType.Password)
    ]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "PasswordMismatch"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed record PendingVerification(
    VerificationRequest Request,
    string UserId,
    DateTimeOffset ExpiresAt,
    bool Verified = false
);

public sealed class MemberDashboard
{
    public MemberProfile Member { get; set; } = null!;
    public List<MemberSubscription> Subscriptions { get; set; } = [];
    public int SavedCount { get; set; }
    public bool BotReady { get; set; }
    public string DeliveryError { get; set; } = "";
}

public sealed class PlanCatalog
{
    public List<MembershipPlan> Plans { get; set; } = [];
    public List<MembershipPlanVersion> Versions { get; set; } = [];
    public List<MemberSubscription> Subscriptions { get; set; } = [];
}

public sealed class PlanInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string NameAr { get; set; } = "";

    [Required(ErrorMessage = "FieldRequired"), StringLength(160)]
    public string NameEn { get; set; } = "";

    [StringLength(2000)]
    public string DescriptionAr { get; set; } = "";

    [StringLength(2000)]
    public string DescriptionEn { get; set; } = "";

    [StringLength(80)]
    public string BadgeAr { get; set; } = "";

    [StringLength(80)]
    public string BadgeEn { get; set; } = "";

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal PriceIls { get; set; } = 5;

    [Range(1, 3650)]
    public int Days { get; set; } = 30;
    public OpportunityKind[] Details { get; set; } = [];
    public OpportunityKind[] Alerts { get; set; } = [];
    public bool Visible { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Version { get; set; }
}

public sealed class PurchaseInput
{
    public int PlanId { get; set; }
    public int PresentedVersionId { get; set; }
    public decimal PresentedAmount { get; set; }
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [StringLength(50)]
    public string DiscountCode { get; set; } = "";
    public string Gateway { get; set; } = "";
    public bool AcceptTerms { get; set; }
    public MembershipPlanVersion? Plan { get; set; }
    public string[] Gateways { get; set; } = [];
    public decimal FinalAmount { get; set; }
    public DateTimeOffset StartsAt { get; set; }
}

public sealed class PreferenceInput
{
    public OpportunityKind[] Kinds { get; set; } = [];
    public string[] Categories { get; set; } = [];
    public string[] Locations { get; set; } = [];
    public string[] AvailableCategories { get; set; } = [];
    public string[] AvailableLocations { get; set; } = [];
    public bool SavedReminders { get; set; }
}
