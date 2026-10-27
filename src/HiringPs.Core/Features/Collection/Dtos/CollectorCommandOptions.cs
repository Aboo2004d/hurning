namespace HiringPs.Core;

/// <summary>خيارات أمر العامل المحلي؛ لا يتحول غياب الأمر إلى خدمة جمع دائمة.</summary>
public sealed record CollectorCommandOptions(bool Collect, bool Checks, int? SourceId, string? SettingsFile,
    string? EnvironmentName, bool Probe, bool InstallBrowser, bool PdfCheck, string? PdfRead, bool Help);
