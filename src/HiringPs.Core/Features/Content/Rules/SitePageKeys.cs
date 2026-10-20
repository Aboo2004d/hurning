namespace HiringPs.Core;

/// <summary>Known content keys shared by public pages and their editors.</summary>
public static class SitePageKeys
{
    public const string Questions = "about-questions";
    /// <summary>Restricts ordinary text editing to the existing about and privacy pages.</summary>
    public static bool IsEditable(string key) => key is "about" or "privacy";
}
