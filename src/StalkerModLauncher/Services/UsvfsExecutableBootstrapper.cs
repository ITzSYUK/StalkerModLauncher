using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed record UsvfsBootstrapResult(
    string ExecutablePath,
    string RootPath,
    string DirectoryPath,
    int FileCount);

internal sealed class UsvfsExecutableBootstrapper
{
    private const string BootstrapDirectoryName = "usvfs-bootstrap";
    private readonly WorkspaceMaterializer _materializer = new();

    public void Clear(string profileWorkspace)
    {
        var bootstrapRoot = Path.Combine(profileWorkspace, "userdata", BootstrapDirectoryName);
        FileSystemSafety.EnsureDirectoryInside(bootstrapRoot, profileWorkspace);
        if (!Directory.Exists(bootstrapRoot))
        {
            return;
        }

        FileSystemSafety.DeleteDirectoryContents(bootstrapRoot, profileWorkspace);
        Directory.Delete(bootstrapRoot);
    }

    public UsvfsBootstrapResult Prepare(
        FileLayerPlan layerPlan,
        UsvfsLaunchTarget launchTarget,
        string profileWorkspace,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        FileSystemSafety.EnsureRelativePath(launchTarget.ExecutableRelativePath, "USVFS executable");
        var executableDirectoryRelative = Path.GetDirectoryName(launchTarget.ExecutableRelativePath) ?? string.Empty;
        var bootstrapRoot = Path.Combine(profileWorkspace, "userdata", BootstrapDirectoryName);
        var bootstrapDirectory = executableDirectoryRelative.Length == 0
            ? bootstrapRoot
            : Path.Combine(bootstrapRoot, executableDirectoryRelative);

        FileSystemSafety.EnsureDirectoryInside(bootstrapRoot, profileWorkspace);
        FileSystemSafety.DeleteDirectoryContents(bootstrapRoot, profileWorkspace);
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

    private static Dictionary<string, string> CollectFinalDirectoryFiles(
        FileLayerPlan layerPlan,
        string executableDirectoryRelative,
        string selectedExecutableName,
        CancellationToken cancellationToken)
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
}
