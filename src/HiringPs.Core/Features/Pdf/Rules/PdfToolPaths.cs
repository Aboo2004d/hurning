using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

// Resolve bundled paths beside the running app (or the shared runtime folder), never against the shell's directory.
/// <summary>يحافظ على قاعدة واحدة لحساب المسارات النسبية من مجلد التطبيق أو مجلد الأدوات المشترك مع دعم PATH.</summary>
public static class PdfToolPaths
{
    /// <summary>
    /// جذر المسارات النسبية لأدوات PDF. افتراضيًا مجلد التطبيق؛ ويمكن جعله مجلدًا ثابتًا على السيرفر
    /// (Pdf:ToolsRoot) حتى لا تُرفع الأدوات مع كل نشر.
    /// </summary>
    public static string Root { get; private set; } = AppContext.BaseDirectory;

    /// <summary>يقرأ Pdf:ToolsRoot مرة واحدة عند بدء التشغيل.</summary>
    public static void Configure(IConfiguration configuration)
    {
        if (configuration["Pdf:ToolsRoot"] is { Length: > 0 } root)
            Root = Path.GetFullPath(root, AppContext.BaseDirectory);
    }

    /// <summary>يحول المسار النسبي إلى مسار كامل انطلاقًا من جذر الأدوات.</summary>
    public static string Directory(string path) => Path.GetFullPath(path, Root);

    /// <summary>يحل مسار الأداة؛ يبقي الاسم المفرد للبحث في PATH.</summary>
    public static string Executable(string path) =>
        Path.IsPathRooted(path) || path.Contains('/') || path.Contains(Path.DirectorySeparatorChar)
            ? Directory(path)
            : path; // A bare command such as pdftotext still uses the system PATH.
}
