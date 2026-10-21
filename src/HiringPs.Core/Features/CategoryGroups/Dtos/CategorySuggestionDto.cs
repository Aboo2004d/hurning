namespace HiringPs.Core;

/// <summary>اقتراح غير معتمد؛ النسبة تعني تشابهًا إملائيًا أو صيغة معروفة وليست موافقة تلقائية.</summary>
public sealed record CategorySuggestionDto(string Label, int? GroupId, string? GroupName, int Percent, bool KnownEquivalent = false);
