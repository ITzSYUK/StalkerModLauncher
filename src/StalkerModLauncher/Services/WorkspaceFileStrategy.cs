namespace StalkerModLauncher.Services;

public static class WorkspaceFileStrategy
{
    private static readonly HashSet<string> MutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cfg", ".ini", ".json", ".log", ".ltx", ".script", ".sav", ".txt", ".xml"
    };

    private static readonly HashSet<string> MutableFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "fsgame.ltx",
        "user.ltx"
    };

    private static readonly HashSet<string> MutableDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "appdata",
        "userdata",
        "_appdata_",
        "logs",
        "savedgames",
        "screenshots"
    };

    public static bool MustCopy(string relativePath)
    {
        var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return MutableFileNames.Contains(Path.GetFileName(normalized)) ||
               MutableExtensions.Contains(Path.GetExtension(normalized)) ||
               segments.Any(MutableDirectoryNames.Contains);
    }
}
