namespace HiringPs.Core;
/// <summary>نتيجة إرسال موعد تلقائي دون تضمين أي رمز وصول.</summary>
public sealed record RandomPublicationDeliveryDto(RandomPublicationState State, string Code);
