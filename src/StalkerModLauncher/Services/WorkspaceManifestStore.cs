using System.Text.Json;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed class WorkspaceManifestStore
{
    private const string ManifestFileName = "build-manifest.json";

    public string? TryGetCachedExecutable(
        string workspaceRoot,
        string currentWorkspace,
        ModProfile profile,
        string buildSignature,
        IProgress<string> progress)
    {
        var manifestPath = Path.Combine(workspaceRoot, ManifestFileName);
        if (!Directory.Exists(currentWorkspace))
        {
            progress.Report("Workspace будет подготовлен: папка current ещё не создана.");
            return null;
        }

        if (!File.Exists(manifestPath))
        {
            progress.Report("Workspace будет подготовлен: кэш сборки отсутствует.");
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<WorkspaceBuildManifest>(File.ReadAllText(manifestPath));
            if (!string.Equals(manifest?.Signature, buildSignature, StringComparison.Ordinal))
            {
                progress.Report("Workspace будет пересобран: изменились файлы, порядок модов или настройки запуска.");
                return null;
            }

            var executablePath = Path.Combine(currentWorkspace, profile.ExecutableRelativePath);
            if (!File.Exists(executablePath))
            {
                progress.Report("Workspace будет пересобран: выбранный EXE отсутствует в текущей сборке.");
                return null;
            }

            progress.Report("Workspace уже актуален: изменений в игре и модах не найдено.");
            return executablePath;
        }
        catch
        {
            progress.Report("Workspace будет пересобран: не удалось прочитать кэш сборки.");
            return null;
        }
    }

    public void Write(string workspaceRoot, string buildSignature, WorkspaceBuildStats stats)
    {
        var manifest = new WorkspaceBuildManifest
        {
            Signature = buildSignature,
            BuiltAtUtc = DateTime.UtcNow,
            FileCount = stats.FileCount,
            HardLinkCount = stats.LinkedFiles,
            SymbolicLinkCount = stats.SymbolicLinkedFiles,
            LocalFileCount = stats.ProtectedCopies,
            LogicalSizeBytes = stats.LogicalSizeBytes,
            PhysicalSizeBytes = stats.PhysicalSizeBytes,
            HasStatistics = true
        };
        File.WriteAllText(
            Path.Combine(workspaceRoot, ManifestFileName),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class WorkspaceBuildManifest
{
    public string Signature { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; }
    public bool HasStatistics { get; set; }
    public int FileCount { get; set; }
    public int HardLinkCount { get; set; }
    public int SymbolicLinkCount { get; set; }
    public int LocalFileCount { get; set; }
    public long LogicalSizeBytes { get; set; }
    public long PhysicalSizeBytes { get; set; }
}
