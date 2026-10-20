namespace HiringPs.Core;

/// <summary>Explicit page write command; null BodyEn preserves the existing English body for the shared questions JSON section.</summary>
public sealed record SitePageWriteDto(
    string Key,
    string TitleAr,
    string BodyAr,
    string TitleEn,
    string? BodyEn,
    byte[]? Version
);
