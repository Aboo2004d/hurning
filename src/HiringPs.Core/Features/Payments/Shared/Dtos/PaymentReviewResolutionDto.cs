namespace HiringPs.Core;

/// <summary>قرار إنهاء مراجعة الدفع؛ لا يمثل تأكيدًا آليًا لإيصال المزود.</summary>
public sealed record PaymentReviewResolutionDto(Guid OrderId, string Actor, string? Result);
