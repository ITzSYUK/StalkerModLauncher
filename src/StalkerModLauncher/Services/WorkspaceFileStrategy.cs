namespace StalkerModLauncher.Services;

public static class WorkspaceFileStrategy
{
    public static bool MustCopy(string relativePath)
    {
        var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.GetFileName(normalized).Equals("fsgame.ltx", StringComparison.OrdinalIgnoreCase) ||
               ProfileWritableGameFiles.Rules.Any(rule =>
                   rule.RelativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
