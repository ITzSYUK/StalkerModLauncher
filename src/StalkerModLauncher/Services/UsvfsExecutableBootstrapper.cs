using System.Security.Cryptography;
using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed record UsvfsBootstrapResult(
    string ExecutablePath,
    string RootPath,
    string DirectoryPath,
    int FileCount);

internal sealed class UsvfsExecutableBootstrapper
{
    private const string LegacyBootstrapDirectoryName = "usvfs-bootstrap";
    private const string AnomalyLauncherFileName = "AnomalyLauncher.exe";
    private readonly WorkspaceMaterializer _materializer = new();

    public void Clear(string profileWorkspace, string bootstrapRoot)
    {
        ClearManagedDirectory(bootstrapRoot, Path.GetDirectoryName(bootstrapRoot)!);

        var legacyBootstrapRoot = Path.Combine(
            profileWorkspace,
            "userdata",
            LegacyBootstrapDirectoryName);
        ClearManagedDirectory(legacyBootstrapRoot, profileWorkspace);
    }

    private static void ClearManagedDirectory(string bootstrapRoot, string allowedRoot)
    {
        FileSystemSafety.EnsureDirectoryInside(bootstrapRoot, allowedRoot);
        if (!Directory.Exists(bootstrapRoot))
        {
            return;
        }

        FileSystemSafety.DeleteDirectoryContents(bootstrapRoot, allowedRoot);
    }

    public UsvfsBootstrapResult Prepare(
        FileLayerPlan layerPlan,
        UsvfsLaunchTarget launchTarget,
        string bootstrapRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        FileSystemSafety.EnsureRelativePath(launchTarget.ExecutableRelativePath, "USVFS executable");
        var executableDirectoryRelative = Path.GetDirectoryName(launchTarget.ExecutableRelativePath) ?? string.Empty;
        var bootstrapDirectory = executableDirectoryRelative.Length == 0
            ? bootstrapRoot
            : Path.Combine(bootstrapRoot, executableDirectoryRelative);

        var bootstrapCacheRoot = Path.GetDirectoryName(bootstrapRoot)!;
        FileSystemSafety.EnsureDirectoryInside(bootstrapRoot, bootstrapCacheRoot);
        FileSystemSafety.DeleteDirectoryContents(bootstrapRoot, bootstrapCacheRoot);
        Directory.CreateDirectory(bootstrapDirectory);

        var selectedRelativeName = Path.GetFileName(launchTarget.ExecutableRelativePath);
        var files = CollectFinalDirectoryFiles(
            layerPlan,
            executableDirectoryRelative,
            selectedRelativeName,
            cancellationToken);
        files[selectedRelativeName] = launchTarget.ExecutablePath;

        var stats = new WorkspaceBuildStats();
        foreach (var file in files.OrderBy(file => file.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _materializer.ReplaceFile(file.Value, bootstrapDirectory, file.Key, stats);
        }

        var executablePath = Path.Combine(bootstrapDirectory, selectedRelativeName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("USVFS bootstrap executable was not created.", executablePath);
        }

        progress?.Report(
            $"USVFS executable bootstrap prepared: {files.Count:N0} linked files in {bootstrapDirectory}");
        return new UsvfsBootstrapResult(executablePath, bootstrapRoot, bootstrapDirectory, files.Count);
    }

    public UsvfsBootstrapResult PrepareAnomalyLauncher(
        FileLayerPlan layerPlan,
        UsvfsLaunchTarget launchTarget,
        string bootstrapRoot,
        string writeOverlayRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        FileSystemSafety.EnsureRelativePath(launchTarget.ExecutableRelativePath, "USVFS Anomaly launcher");
        if (!launchTarget.ExecutableRelativePath.Equals(AnomalyLauncherFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Anomaly launcher bootstrap requires {AnomalyLauncherFileName}: {launchTarget.ExecutableRelativePath}");
        }

        var bootstrapCacheRoot = Path.GetDirectoryName(bootstrapRoot)!;
        FileSystemSafety.EnsureDirectoryInside(bootstrapRoot, bootstrapCacheRoot);
        FileSystemSafety.DeleteDirectoryContents(bootstrapRoot, bootstrapCacheRoot);
        Directory.CreateDirectory(bootstrapRoot);

        var stats = new WorkspaceBuildStats();
        var rootFiles = CollectFinalDirectoryFiles(
            layerPlan,
            executableDirectoryRelative: string.Empty,
            AnomalyLauncherFileName,
            cancellationToken);
        foreach (var excludedFile in AnomalyLauncherExcludedRootFiles)
        {
            rootFiles.Remove(excludedFile);
        }

        MaterializeFiles(rootFiles, bootstrapRoot, string.Empty, stats, cancellationToken);

        var binFiles = CollectFinalDirectoryFiles(
            layerPlan,
            executableDirectoryRelative: "bin",
            selectedExecutableName: string.Empty,
            cancellationToken,
            includeAllExecutables: true);
        MaterializeFiles(binFiles, bootstrapRoot, "bin", stats, cancellationToken);

        foreach (var mutableFile in AnomalyLauncherMutableFiles)
        {
            var profileFile = EnsureProfileLauncherFile(layerPlan, writeOverlayRoot, mutableFile);
            if (profileFile is not null)
            {
                _materializer.ReplaceFile(profileFile, bootstrapRoot, mutableFile, stats);
            }
        }

        var executablePath = Path.Combine(bootstrapRoot, AnomalyLauncherFileName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("USVFS Anomaly launcher bootstrap executable was not created.", executablePath);
        }

        progress?.Report(
            $"USVFS Anomaly launcher bootstrap prepared: {stats.FileCount:N0} linked files in {bootstrapRoot}");
        return new UsvfsBootstrapResult(executablePath, bootstrapRoot, bootstrapRoot, stats.FileCount);
    }

    private void MaterializeFiles(
        IReadOnlyDictionary<string, string> files,
        string bootstrapRoot,
        string directoryRelative,
        WorkspaceBuildStats stats,
        CancellationToken cancellationToken)
    {
        foreach (var file in files.OrderBy(file => file.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = directoryRelative.Length == 0
                ? file.Key
                : Path.Combine(directoryRelative, file.Key);
            _materializer.ReplaceFile(file.Value, bootstrapRoot, relativePath, stats);
        }
    }

    private static string? EnsureProfileLauncherFile(
        FileLayerPlan layerPlan,
        string writeOverlayRoot,
        string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Anomaly launcher profile file");
        var profileFile = FileSystemSafety.ResolvePathInside(
            writeOverlayRoot,
            relativePath,
            "Anomaly launcher profile file");
        if (File.Exists(profileFile))
        {
            return profileFile;
        }

        var source = layerPlan.FindFinalFile(relativePath);
        if (source is null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(profileFile)!);
        File.Copy(source.FullPath, profileFile, overwrite: false);
        var attributes = File.GetAttributes(profileFile);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(profileFile, attributes & ~FileAttributes.ReadOnly);
        }

        return profileFile;
    }

    private static Dictionary<string, string> CollectFinalDirectoryFiles(
        FileLayerPlan layerPlan,
        string executableDirectoryRelative,
        string selectedExecutableName,
        CancellationToken cancellationToken,
        bool includeAllExecutables = false)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layerPlan.SourceLayers.Where(layer => Directory.Exists(layer.RootPath)))
        {
            var sourceDirectory = executableDirectoryRelative.Length == 0
                ? layer.RootPath
                : Path.Combine(layer.RootPath, executableDirectoryRelative);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extension = Path.GetExtension(sourceFile);
                if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                    !includeAllExecutables &&
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(sourceFile).Equals(selectedExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                files[Path.GetFileName(sourceFile)] = sourceFile;
            }
        }

        return files;
    }

    private static IReadOnlyList<string> AnomalyLauncherMutableFiles { get; } =
    [
        "AnomalyLauncher.cfg",
        "commandline.txt"
    ];

    private static IReadOnlyList<string> AnomalyLauncherExcludedRootFiles { get; } =
    [
        .. AnomalyLauncherMutableFiles,
        "fsgame.ltx"
    ];
}

internal static class UsvfsBootstrapPathResolver
{
    private const string BootstrapDirectoryName = ".usvfs-bootstrap";

    public static void DeleteProfile(string profileWorkspace, string baseGameRoot, string profileId)
    {
        var profileRoot = Resolve(profileWorkspace, baseGameRoot, profileId);
        FileSystemSafety.DeleteDirectoryContents(profileRoot, profileWorkspace);
        DeleteLegacySharedProfile(profileWorkspace, baseGameRoot, profileId);
    }

    public static string Resolve(string profileWorkspace, string baseGameRoot, string profileId)
    {
        var profileRoot = Path.Combine(
            Path.GetFullPath(profileWorkspace),
            BootstrapDirectoryName);
        if (!IsAscii(profileRoot))
        {
            throw new InvalidOperationException(
                "USVFS requires an ASCII-only profile workspace. Move the profile workspace to a path without non-Latin characters.");
        }

        return profileRoot;
    }

    public static void DeleteLegacySharedProfile(
        string profileWorkspace,
        string baseGameRoot,
        string profileId)
    {
        foreach (var legacyRoot in GetLegacySharedRoots(profileWorkspace, baseGameRoot))
        {
            var profileRoot = Path.Combine(legacyRoot, GetProfileKey(profileId));
            FileSystemSafety.DeleteDirectoryContents(profileRoot, legacyRoot);
            if (Directory.Exists(legacyRoot) &&
                !Directory.EnumerateFileSystemEntries(legacyRoot).Any())
            {
                Directory.Delete(legacyRoot);
            }
        }
    }

    private static IEnumerable<string> GetLegacySharedRoots(
        string profileWorkspace,
        string baseGameRoot)
    {
        var workspaceParent = Directory.GetParent(Path.GetFullPath(profileWorkspace))?.FullName;
        if (!string.IsNullOrWhiteSpace(workspaceParent))
        {
            yield return Path.Combine(workspaceParent, BootstrapDirectoryName);
        }

        if (!string.IsNullOrWhiteSpace(baseGameRoot))
        {
            var volumeRoot = Path.GetPathRoot(Path.GetFullPath(baseGameRoot));
            if (!string.IsNullOrWhiteSpace(volumeRoot))
            {
                var volumeCacheRoot = Path.Combine(
                    volumeRoot,
                    "StalkerModLauncher",
                    "UsvfsBootstrap");
                if (workspaceParent is null ||
                    !FileSystemSafety.IsSameDirectory(
                        volumeCacheRoot,
                        Path.Combine(workspaceParent, BootstrapDirectoryName)))
                {
                    yield return volumeCacheRoot;
                }
            }
        }
    }

    private static string GetProfileKey(string profileId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(profileId));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static bool IsAscii(string path) => path.All(character => character <= 0x7F);
}
