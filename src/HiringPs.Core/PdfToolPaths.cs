namespace HiringPs.Core;

// Resolve bundled paths beside the running worker, never against the shell's directory.
public static class PdfToolPaths
{
    public static string Directory(string path) => Path.GetFullPath(path, AppContext.BaseDirectory);

    public static string Executable(string path) =>
        Path.IsPathRooted(path) || path.Contains('/') || path.Contains(Path.DirectorySeparatorChar)
            ? Directory(path)
            : path; // A bare command such as pdftotext still uses the system PATH.
}
