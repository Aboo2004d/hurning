namespace HiringPs.Core;

/// <summary>حالة طلب تجهيز منشور مع بياناته عندما يكون العرض متاحًا.</summary>
public sealed record SocialComposeReadResultDto(SocialComposeReadStatus Status, SocialComposeDataDto? Data = null);
