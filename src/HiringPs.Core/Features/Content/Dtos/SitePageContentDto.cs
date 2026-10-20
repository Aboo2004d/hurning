namespace HiringPs.Core;

/// <summary>Content snapshot for public rendering and versioned administration edits.</summary>
public sealed record SitePageContentDto(
    string Key,
    string TitleAr,
    string BodyAr,
    string TitleEn,
    string BodyEn,
    byte[] Version
);
