namespace HiringPs.Core;

// Resolve bundled paths beside the running worker, never against the shell's directory.
/// <summary>يحافظ على قاعدة واحدة لحساب المسارات النسبية من مجلد التطبيق مع دعم PATH.</summary>
public static class PdfToolPaths
{
    /// <summary>يحول المسار النسبي إلى مسار كامل انطلاقًا من مجلد التطبيق.</summary>
    public static string Directory(string path) => Path.GetFullPath(path, AppContext.BaseDirectory);

    /// <summary>يحل مسار الأداة؛ يبقي الاسم المفرد للبحث في PATH.</summary>
    public static string Executable(string path) =>
        Path.IsPathRooted(path) || path.Contains('/') || path.Contains(Path.DirectorySeparatorChar)
            ? Directory(path)
            : path; // A bare command such as pdftotext still uses the system PATH.
}
