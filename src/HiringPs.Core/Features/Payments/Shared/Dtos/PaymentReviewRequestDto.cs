namespace HiringPs.Core;

/// <summary>طلب مراجعة دفعة من مالكها؛ ينشئه الخادم ولا يستقبل هوية المالك من النموذج.</summary>
public sealed record PaymentReviewRequestDto(Guid OrderId, string OwnerId, string? Note, bool ReturnedFromProvider);
