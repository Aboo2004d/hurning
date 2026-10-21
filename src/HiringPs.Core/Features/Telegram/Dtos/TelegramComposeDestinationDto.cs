namespace HiringPs.Core;

/// <summary>قناة معتمدة للفرصة وحالة المنشور السابق فيها دون بيانات اعتماد البوت.</summary>
public sealed record TelegramComposeDestinationDto(string Target, string Channel, bool Available, string? Status);
