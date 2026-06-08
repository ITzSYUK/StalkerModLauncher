using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public interface IProfileWorkspaceManager
{
    void DeleteProfileWorkspace(ModProfile profile, string gamePath);
}

public sealed class WorkspaceBuilder : IProfileWorkspaceManager
{
    private const string MarkerFileName = ".stalker-launcher-workspace";
    private const string ManifestFileName = "build-manifest.json";
    private readonly AppPaths _paths;

    public WorkspaceBuilder(AppPaths paths)
    {
        _paths = paths;
    }

    public Task<WorkspaceBuildResult> BuildAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Build(gamePath, profile, progress, cancellationToken), cancellationToken);
    }

    private WorkspaceBuildResult Build(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Launch executable");

        if (profile.IsStandalone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return BuildStandalone(profile, gamePath, progress, cancellationToken);
        }

        if (!Directory.Exists(gamePath))
        {
            throw new DirectoryNotFoundException($"Game folder was not found: {gamePath}");
        }

        var workspaceRoot = EnsureProfileWorkspace(profile, gamePath);
        var currentWorkspace = Path.Combine(workspaceRoot, "current");
        FileSystemSafety.EnsureDirectoryInside(currentWorkspace, workspaceRoot);
        progress.Report("Checking game and mod files...");
        var sourceSnapshot = CaptureSources(gamePath, profile, cancellationToken);
        var buildSignature = CreateBuildSignature(profile, sourceSnapshot);
        var cachedExecutable = TryUseCachedWorkspace(workspaceRoot, currentWorkspace, profile, buildSignature, progress);
        if (cachedExecutable is not null)
        {
            return new WorkspaceBuildResult(currentWorkspace, cachedExecutable);
        }

        progress.Report("Preparing clean profile workspace...");
        FileSystemSafety.DeleteDirectoryContents(currentWorkspace, workspaceRoot);
        Directory.CreateDirectory(currentWorkspace);

        var stats = new WorkspaceBuildStats();

        progress.Report("Linking base game into isolated workspace...");
        MirrorBaseGame(sourceSnapshot.Game, currentWorkspace, progress, stats, cancellationToken);

        foreach (var mod in profile.Mods.Where(mod => mod.IsEnabled).OrderBy(mod => mod.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyMod(currentWorkspace, mod, sourceSnapshot.Mods[mod.Id], progress, stats, cancellationToken);
        }

        ResolveWorkingDirectory(gamePath, currentWorkspace, workspaceRoot, profile, progress);

        var executablePath = Path.Combine(currentWorkspace, profile.ExecutableRelativePath);
        if (!File.Exists(executablePath))
        {
            var detectedExecutable = TryDetectExecutable(currentWorkspace, profile.ExecutableRelativePath, progress);
            if (detectedExecutable is null)
            {
                var discovered = Directory.EnumerateFiles(currentWorkspace, "*.exe", SafeEnumerationOptions)
                    .Select(path => Path.GetRelativePath(currentWorkspace, path))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray();
                var details = discovered.Length == 0
                    ? "No executable files were found in the built workspace."
                    : $"Executables found: {string.Join(", ", discovered)}";

                throw new FileNotFoundException(
                    $"Profile executable was not found after workspace build: {profile.ExecutableRelativePath}. {details}",
                    executablePath);
            }

            executablePath = detectedExecutable;
            profile.ExecutableRelativePath = Path.GetRelativePath(currentWorkspace, detectedExecutable);
        }

        progress.Report($"Workspace is ready. Linked: {stats.LinkedFiles:N0}, symlinked: {stats.SymbolicLinkedFiles:N0}, protected copies: {stats.ProtectedCopies:N0}, copied fallback: {stats.CopiedFiles:N0}.");
        WriteBuildManifest(workspaceRoot, buildSignature);
        return new WorkspaceBuildResult(currentWorkspace, executablePath);
    }

    public string GetSavedGamesPath(ModProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            return string.Empty;
        }

        return Path.Combine(profile.WorkspacePath, "userdata", "savedgames");
    }

    public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkspacePath) || !Directory.Exists(profile.WorkspacePath))
        {
            return;
        }

        var workspacePath = Path.GetFullPath(profile.WorkspacePath);
        var allowedRoot = _paths.GetManagedWorkspaceRoots(gamePath)
            .Select(Path.GetFullPath)
            .FirstOrDefault(root =>
                FileSystemSafety.IsDirectoryInside(workspacePath, root) &&
                !FileSystemSafety.IsSameDirectory(workspacePath, root));

        if (allowedRoot is null)
        {
            throw new InvalidOperationException($"Refusing to delete workspace outside managed launcher roots: {workspacePath}");
        }

        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            throw new InvalidOperationException($"Refusing to delete profile workspace without launcher marker file: {workspacePath}");
        }

        FileSystemSafety.DeleteDirectoryContents(workspacePath, allowedRoot);
    }

    private string EnsureProfileWorkspace(ModProfile profile, string gamePath)
    {
        var preferredRoot = _paths.GetPreferredWorkspaceRoot(gamePath);
        var managedRoots = _paths.GetManagedWorkspaceRoots(gamePath);

        var workspacePath = string.IsNullOrWhiteSpace(profile.WorkspacePath)
            ? Path.Combine(preferredRoot, $"{FileSystemSafety.SanitizeName(profile.Name)}-{profile.Id}")
            : profile.WorkspacePath;

        if (!string.IsNullOrWhiteSpace(profile.WorkspacePath) &&
            !string.IsNullOrWhiteSpace(gamePath) &&
            !FileSystemSafety.IsSameDirectory(Path.GetPathRoot(workspacePath)!, Path.GetPathRoot(preferredRoot)!))
        {
            workspacePath = Path.Combine(preferredRoot, $"{FileSystemSafety.SanitizeName(profile.Name)}-{profile.Id}");
        }

        if (!managedRoots.Any(root =>
                FileSystemSafety.IsDirectoryInside(workspacePath, root) &&
                !FileSystemSafety.IsSameDirectory(workspacePath, root)))
        {
            throw new InvalidOperationException($"Profile workspace must be a profile-specific folder inside a managed launcher workspace root: {workspacePath}");
        }

        Directory.CreateDirectory(workspacePath);

        var markerPath = Path.Combine(workspacePath, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            File.WriteAllText(markerPath, "Managed by Stalker Mod Launcher. It is safe for the launcher to recreate the 'current' subfolder.");
        }

        profile.WorkspacePath = workspacePath;
        return workspacePath;
    }

    private static void MirrorBaseGame(
        DirectorySnapshot source,
        string targetRoot,
        IProgress<string> progress,
        WorkspaceBuildStats stats,
        CancellationToken cancellationToken)
    {
        foreach (var relativePath in source.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(targetRoot, relativePath));
        }

        var fileCount = 0;
        foreach (var file in source.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(targetRoot, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            LinkOrCopyFile(file.FullPath, targetFile, file.RelativePath, stats);

            fileCount++;
            if (fileCount % 500 == 0)
            {
                progress.Report($"Linked {fileCount:N0} base files...");
            }
        }
    }

    private static string? TryUseCachedWorkspace(
        string workspaceRoot,
        string currentWorkspace,
        ModProfile profile,
        string buildSignature,
        IProgress<string> progress)
    {
        var manifestPath = Path.Combine(workspaceRoot, ManifestFileName);
        if (!Directory.Exists(currentWorkspace) || !File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<WorkspaceBuildManifest>(File.ReadAllText(manifestPath));
            if (!string.Equals(manifest?.Signature, buildSignature, StringComparison.Ordinal))
            {
                return null;
            }

            var executablePath = Path.Combine(currentWorkspace, profile.ExecutableRelativePath);
            if (!File.Exists(executablePath))
            {
                return null;
            }

            progress.Report("Using cached profile workspace. No file overlay changes detected.");
            return executablePath;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteBuildManifest(string workspaceRoot, string buildSignature)
    {
        var manifestPath = Path.Combine(workspaceRoot, ManifestFileName);
        var manifest = new WorkspaceBuildManifest
        {
            Signature = buildSignature,
            BuiltAtUtc = DateTime.UtcNow
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static WorkspaceSourceSnapshot CaptureSources(
        string gamePath,
        ModProfile profile,
        CancellationToken cancellationToken)
    {
        var game = CaptureDirectory(gamePath, cancellationToken);
        var mods = new Dictionary<string, DirectorySnapshot>();
        foreach (var mod in profile.Mods.Where(mod => mod.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(mod.SourcePath))
            {
                throw new DirectoryNotFoundException($"Mod folder was not found: {mod.SourcePath}");
            }

            mods.Add(mod.Id, CaptureDirectory(mod.SourcePath, cancellationToken));
        }

        return new WorkspaceSourceSnapshot(game, mods);
    }

    private static DirectorySnapshot CaptureDirectory(string directoryPath, CancellationToken cancellationToken)
    {
        var fullRoot = Path.GetFullPath(directoryPath);
        var directories = Directory.EnumerateDirectories(fullRoot, "*", SafeEnumerationOptions)
            .Select(path => Path.GetRelativePath(fullRoot, path))
            .ToArray();
        var files = Directory.EnumerateFiles(fullRoot, "*", SafeEnumerationOptions)
            .Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                return new SourceFileSnapshot(
                    path,
                    Path.GetRelativePath(fullRoot, path),
                    info.Length,
                    info.LastWriteTimeUtc.Ticks);
            })
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new DirectorySnapshot(fullRoot, directories, files);
    }

    private static string CreateBuildSignature(ModProfile profile, WorkspaceSourceSnapshot sourceSnapshot)
    {
        var builder = new StringBuilder();
        AppendDirectoryFingerprint(builder, sourceSnapshot.Game);
        builder.AppendLine(profile.ExecutableRelativePath);
        builder.AppendLine(profile.IsStandalone ? "standalone" : "overlay");

        foreach (var mod in profile.Mods.OrderBy(mod => mod.Order))
        {
            builder.Append(mod.Order).Append('|')
                .Append(mod.IsEnabled).Append('|')
                .Append(Path.GetFullPath(mod.SourcePath)).AppendLine();

            if (sourceSnapshot.Mods.TryGetValue(mod.Id, out var modSnapshot))
            {
                AppendDirectoryFingerprint(builder, modSnapshot);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendDirectoryFingerprint(StringBuilder builder, DirectorySnapshot snapshot)
    {
        builder.AppendLine(snapshot.RootPath);

        foreach (var file in snapshot.Files)
        {
            builder.Append(file.RelativePath).Append('|')
                .Append(file.Length).Append('|')
                .Append(file.LastWriteTimeUtcTicks).AppendLine();
        }
    }

    private static void ApplyMod(
        string workspaceRoot,
        ModEntry mod,
        DirectorySnapshot source,
        IProgress<string> progress,
        WorkspaceBuildStats stats,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(mod.SourcePath))
        {
            throw new DirectoryNotFoundException($"Mod folder was not found: {mod.SourcePath}");
        }

        progress.Report($"Applying mod: {mod.Name}");

        var directoryCount = 0;
        var fileCount = 0;

        foreach (var relativePath in source.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(workspaceRoot, relativePath));
            directoryCount++;
        }

        foreach (var file in source.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemSafety.EnsureRelativePath(file.RelativePath, "Mod file");
            var targetFile = Path.Combine(workspaceRoot, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            // The base mirror may use links. Deleting the workspace entry before overlaying a mod file
            // guarantees that the new link or copy points at the mod file, not at the original GOG file.
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            LinkOrCopyFile(file.FullPath, targetFile, file.RelativePath, stats);
            fileCount++;
        }

        progress.Report($"Applied mod '{mod.Name}': {fileCount:N0} files, {directoryCount:N0} folders.");
        if (fileCount == 0)
        {
            progress.Report($"Warning: mod '{mod.Name}' did not contain files visible to the launcher. Check that the profile points to the mod root folder, not an empty wrapper folder.");
        }
    }

    private static string? TryDetectExecutable(string workspaceRoot, string requestedRelativePath, IProgress<string> progress)
    {
        var requestedName = Path.GetFileName(requestedRelativePath);
        var candidates = Directory.EnumerateFiles(workspaceRoot, "*.exe", SafeEnumerationOptions)
            .Select(path => new
            {
                FullPath = path,
                RelativePath = Path.GetRelativePath(workspaceRoot, path),
                Name = Path.GetFileName(path)
            })
            .OrderBy(candidate => GetExecutableRank(candidate.RelativePath, requestedName))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var best = candidates.FirstOrDefault();
        if (best is null)
        {
            return null;
        }

        var rank = GetExecutableRank(best.RelativePath, requestedName);
        if (rank > 50 && candidates.Length != 1)
        {
            return null;
        }

        progress.Report($"Requested executable '{requestedRelativePath}' was not found. Using detected executable '{best.RelativePath}'.");
        return best.FullPath;
    }

    private static int GetExecutableRank(string relativePath, string requestedName)
    {
        var normalized = relativePath.Replace('/', '\\');
        var fileName = Path.GetFileName(normalized);

        if (!string.IsNullOrWhiteSpace(requestedName) &&
            fileName.Equals(requestedName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalized.Equals(@"bin_x64\xrEngine.exe", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (normalized.Equals(@"bin\xrEngine.exe", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalized.Equals(@"bin\xr_3da.exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(@"bin\XR_3DA.exe", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (fileName.Contains("xrEngine", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("OGSR", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (fileName.Contains("xr", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return 100;
    }

    private static void ResolveWorkingDirectory(string gamePath, string currentWorkspace, string profileWorkspace, ModProfile profile, IProgress<string> progress)
    {
        var fsgameName = "fsgame.ltx";
        var fsgameDir = FindFileDirectory(currentWorkspace, fsgameName);
        if (fsgameDir is null)
        {
            progress.Report($"Warning: {fsgameName} not found in workspace. The game may not start correctly.");
            return;
        }

        var relativeDir = Path.GetRelativePath(currentWorkspace, fsgameDir);
        if (relativeDir != ".")
        {
            profile.WorkingDirectoryRelative = relativeDir;
            progress.Report($"Detected {fsgameName} in '{relativeDir}' — using as working directory.");
        }
        else
        {
            profile.WorkingDirectoryRelative = string.Empty;
        }

        var fsgamePath = Path.Combine(fsgameDir, fsgameName);
        var profileDataPath = Path.Combine(profileWorkspace, "userdata");
        Directory.CreateDirectory(profileDataPath);

        CopyUserLtxFromGame(gamePath, profileDataPath, progress);

        var lines = File.ReadAllLines(fsgamePath, Encoding.Default);
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = $"$app_data_root$ = true | false| {profileDataPath}";
                break;
            }
        }

        File.Delete(fsgamePath);
        File.WriteAllLines(fsgamePath, lines, Encoding.Default);
        progress.Report("fsgame.ltx rewritten for profile-local saves and logs.");
    }

    private static void CopyUserLtxFromGame(string gamePath, string profileDataPath, IProgress<string> progress)
    {
        var searchPaths = new List<string>();

        var gameFsgame = FindFileDirectory(gamePath, "fsgame.ltx");
        if (gameFsgame is not null)
        {
            var gameFsgamePath = Path.Combine(gameFsgame, "fsgame.ltx");
            foreach (var line in File.ReadAllLines(gameFsgamePath, Encoding.Default))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 3)
                    {
                        var appDataRelative = parts[2].Trim();
                        if (!string.IsNullOrWhiteSpace(appDataRelative))
                        {
                            var resolved = Path.GetFullPath(Path.Combine(gameFsgame, appDataRelative));
                            if (Directory.Exists(resolved))
                            {
                                searchPaths.Add(resolved);
                            }
                        }
                    }
                    break;
                }
            }
        }

        searchPaths.Add(Path.Combine(gamePath, "appdata"));
        searchPaths.Add(Path.Combine(gamePath, "userdata"));
        searchPaths.Add(Path.Combine(gamePath, "bin", "_appdata_"));

        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var userLtx = Path.Combine(dir, "user.ltx");
            if (!File.Exists(userLtx))
            {
                continue;
            }

            try
            {
                var dest = Path.Combine(profileDataPath, "user.ltx");
                if (File.Exists(dest))
                {
                    progress.Report("Keeping existing profile-local user.ltx.");
                    return;
                }

                File.Copy(userLtx, dest, overwrite: false);
                progress.Report($"Copied user.ltx from {userLtx}");
                return;
            }
            catch (Exception ex)
            {
                progress.Report($"Warning: could not copy user.ltx from {userLtx}: {ex.Message}");
            }
        }
    }

    private static string? FindFileDirectory(string searchRoot, string fileName)
    {
        var rootFile = Path.Combine(searchRoot, fileName);
        if (File.Exists(rootFile))
        {
            return searchRoot;
        }

        foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return dir;
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", SearchOption.TopDirectoryOnly))
        {
            foreach (var subDir in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var candidate = Path.Combine(subDir, fileName);
                if (File.Exists(candidate))
                {
                    return subDir;
                }
            }
        }

        return null;
    }

    private static void LinkOrCopyFile(string sourceFile, string targetFile, string relativePath, WorkspaceBuildStats stats)
    {
        if (WorkspaceFileStrategy.MustCopy(relativePath))
        {
            File.Copy(sourceFile, targetFile, overwrite: false);
            stats.ProtectedCopies++;
            return;
        }

        if (TryCreateHardLink(targetFile, sourceFile))
        {
            stats.LinkedFiles++;
            return;
        }

        if (TryCreateSymbolicFileLink(targetFile, sourceFile))
        {
            stats.SymbolicLinkedFiles++;
            return;
        }

        File.Copy(sourceFile, targetFile, overwrite: false);
        stats.CopiedFiles++;
    }

    private static bool TryCreateHardLink(string targetFile, string existingFile)
    {
        try
        {
            return CreateHardLink(targetFile, existingFile, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateSymbolicFileLink(string targetFile, string existingFile)
    {
        try
        {
            const int fileSymbolicLink = 0x0;
            const int allowUnprivilegedCreate = 0x2;
            return CreateSymbolicLink(targetFile, existingFile, fileSymbolicLink | allowUnprivilegedCreate);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private WorkspaceBuildResult BuildStandalone(
        ModProfile profile,
        string gamePath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var modRoot = profile.Mods.FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))?.SourcePath;
        if (modRoot is null)
        {
            throw new InvalidOperationException(
                "Standalone profile has no enabled mod with a valid folder. Add a mod and enable it.");
        }

        modRoot = Path.GetFullPath(modRoot);
        var exePath = FileSystemSafety.ResolvePathInside(modRoot, profile.ExecutableRelativePath, "Launch executable");

        if (!File.Exists(exePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = Directory.EnumerateFiles(modRoot, "*.exe", SearchOption.AllDirectories)
                .Select(p =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Path.GetRelativePath(modRoot, p);
                })
                .OrderBy(p => p.Equals(profile.ExecutableRelativePath, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (found is null)
            {
                throw new FileNotFoundException(
                    $"No executable found in standalone mod folder: {modRoot}", exePath);
            }

            profile.ExecutableRelativePath = found;
            exePath = Path.Combine(modRoot, found);
        }

        var fsgameDir = FindFileDirectory(modRoot, "fsgame.ltx");
        if (fsgameDir is not null)
        {
            var relativeDir = Path.GetRelativePath(modRoot, fsgameDir);
            if (relativeDir != ".")
            {
                profile.WorkingDirectoryRelative = relativeDir;
            }
        }

        progress.Report($"Standalone mod ready: {profile.Name}");
        return new WorkspaceBuildResult(modRoot, exePath);
    }
}

public sealed record WorkspaceBuildResult(string WorkspaceRoot, string ExecutablePath);

internal sealed class WorkspaceBuildStats
{
    public int LinkedFiles { get; set; }
    public int SymbolicLinkedFiles { get; set; }
    public int ProtectedCopies { get; set; }
    public int CopiedFiles { get; set; }
}

internal sealed class WorkspaceBuildManifest
{
    public string Signature { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; }
}

internal sealed record WorkspaceSourceSnapshot(
    DirectorySnapshot Game,
    IReadOnlyDictionary<string, DirectorySnapshot> Mods);

internal sealed record DirectorySnapshot(
    string RootPath,
    IReadOnlyList<string> Directories,
    IReadOnlyList<SourceFileSnapshot> Files);

internal sealed record SourceFileSnapshot(
    string FullPath,
    string RelativePath,
    long Length,
    long LastWriteTimeUtcTicks);
