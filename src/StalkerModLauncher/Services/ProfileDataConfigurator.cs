namespace StalkerModLauncher.Services;

internal sealed class ProfileDataConfigurator
{
    public string Configure(string gamePath, string currentWorkspace, string profileWorkspace, IProgress<string> progress)
    {
        var fsgameDir = FindFileDirectory(currentWorkspace, "fsgame.ltx");
        if (fsgameDir is null)
        {
            progress.Report("Warning: fsgame.ltx not found in workspace. The game may not start correctly.");
            return string.Empty;
        }

        var relativeDir = Path.GetRelativePath(currentWorkspace, fsgameDir);
        var workingDirectoryRelative = relativeDir == "." ? string.Empty : relativeDir;
        if (workingDirectoryRelative.Length > 0)
        {
            progress.Report($"Detected fsgame.ltx in '{relativeDir}' — using as working directory.");
        }

        var profileDataPath = Path.Combine(profileWorkspace, "userdata");
        Directory.CreateDirectory(profileDataPath);
        CopyUserLtxFromGame(gamePath, profileDataPath, progress);

        var fsgamePath = Path.Combine(fsgameDir, "fsgame.ltx");
        var lines = File.ReadAllLines(fsgamePath, XRayTextEncoding.Config);
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = $"$app_data_root$ = true | false| {profileDataPath}";
                break;
            }
        }

        File.Delete(fsgamePath);
        File.WriteAllLines(fsgamePath, lines, XRayTextEncoding.Config);
        progress.Report("fsgame.ltx rewritten for profile-local saves and logs.");
        return workingDirectoryRelative;
    }

    public string? FindFileDirectory(string searchRoot, string fileName)
    {
        var rootFile = Path.Combine(searchRoot, fileName);
        if (File.Exists(rootFile)) return searchRoot;
        foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.Combine(dir, fileName))) return dir;
        }
        foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", SearchOption.TopDirectoryOnly))
        foreach (var subDir in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.Combine(subDir, fileName))) return subDir;
        }
        return null;
    }

    private void CopyUserLtxFromGame(string gamePath, string profileDataPath, IProgress<string> progress)
    {
        var searchPaths = new List<string>();
        var gameFsgame = FindFileDirectory(gamePath, "fsgame.ltx");
        if (gameFsgame is not null)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(gameFsgame, "fsgame.ltx"), XRayTextEncoding.Config))
            {
                if (!line.TrimStart().StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length >= 3)
                {
                    var resolved = Path.GetFullPath(Path.Combine(gameFsgame, parts[2].Trim()));
                    if (Directory.Exists(resolved)) searchPaths.Add(resolved);
                }
                break;
            }
        }

        searchPaths.AddRange([
            Path.Combine(gamePath, "appdata"),
            Path.Combine(gamePath, "userdata"),
            Path.Combine(gamePath, "bin", "_appdata_")
        ]);

        foreach (var directory in searchPaths.Where(Directory.Exists))
        {
            var source = Path.Combine(directory, "user.ltx");
            if (!File.Exists(source)) continue;

            try
            {
                var destination = Path.Combine(profileDataPath, "user.ltx");
                if (File.Exists(destination))
                {
                    progress.Report("Keeping existing profile-local user.ltx.");
                    return;
                }

                File.Copy(source, destination, overwrite: false);
                progress.Report($"Copied user.ltx from {source}");
                return;
            }
            catch (Exception ex)
            {
                progress.Report($"Warning: could not copy user.ltx from {source}: {ex.Message}");
            }
        }
    }
}
