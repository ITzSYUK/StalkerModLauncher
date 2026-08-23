using System.Text.Json;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal static class WorkspaceManifestStore
{
    private const string ManifestFileName = "build-manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string? TryGetCachedExecutable(
        string workspaceRoot,
        string currentWorkspace,
        ModProfile profile,
        WorkspaceBuildFingerprint buildFingerprint,
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
            if (!string.Equals(manifest?.Signature, buildFingerprint.Signature, StringComparison.Ordinal))
            {
                ReportFingerprintChanges(manifest?.Fingerprint, buildFingerprint, progress);
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

    public static void Write(
        string workspaceRoot,
        WorkspaceBuildFingerprint buildFingerprint,
        WorkspaceBuildStats stats)
    {
        var manifest = new WorkspaceBuildManifest
        {
            Signature = buildFingerprint.Signature,
            Fingerprint = buildFingerprint,
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
            JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static void ReportFingerprintChanges(
        WorkspaceBuildFingerprint? previous,
        WorkspaceBuildFingerprint current,
        IProgress<string> progress)
    {
        var changes = DescribeFingerprintChanges(previous, current);
        progress.Report($"Workspace будет пересобран: {changes[0]}.");
        foreach (var change in changes.Skip(1))
        {
            progress.Report($"Дополнительная причина пересборки: {change}.");
        }
    }

    internal static IReadOnlyList<string> DescribeFingerprintChanges(
        WorkspaceBuildFingerprint? previous,
        WorkspaceBuildFingerprint current)
    {
        if (previous is null)
        {
            return ["обновлён формат диагностической подписи Workspace"];
        }

        var changes = new List<string>();
        if (!string.Equals(previous.FormatVersion, current.FormatVersion, StringComparison.Ordinal))
        {
            changes.Add($"изменился формат Workspace: {previous.FormatVersion} → {current.FormatVersion}");
        }

        if (!string.Equals(
                previous.ExecutableRelativePath,
                current.ExecutableRelativePath,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(
                $"сменился файл запуска: {DisplayValue(previous.ExecutableRelativePath)} → {DisplayValue(current.ExecutableRelativePath)}");
        }

        if (!string.Equals(
                previous.ExecutableSourcePath,
                current.ExecutableSourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(
                $"сменился источник EXE: {DisplayValue(previous.ExecutableSourcePath, "авто")} → {DisplayValue(current.ExecutableSourcePath, "авто")}");
        }

        if (!string.Equals(previous.ProfileMode, current.ProfileMode, StringComparison.Ordinal))
        {
            changes.Add($"сменился тип профиля: {previous.ProfileMode} → {current.ProfileMode}");
        }

        AddLayerChanges(previous.Layers, current.Layers, changes);
        AddSourceChanges(previous.Sources, current.Sources, changes);
        if (changes.Count == 0)
        {
            changes.Add("изменились входные данные Workspace");
        }

        const int maximumReportedChanges = 6;
        if (changes.Count <= maximumReportedChanges)
        {
            return changes;
        }

        return
        [
            .. changes.Take(maximumReportedChanges),
            $"не показано дополнительных изменений: {changes.Count - maximumReportedChanges:N0}"
        ];
    }

    private static void AddLayerChanges(
        IReadOnlyList<WorkspaceBuildLayerFingerprint> previous,
        IReadOnlyList<WorkspaceBuildLayerFingerprint> current,
        List<string> changes)
    {
        var previousIds = previous.Select(layer => layer.Id).ToArray();
        var currentIds = current.Select(layer => layer.Id).ToArray();
        if (!previousIds.ToHashSet(StringComparer.Ordinal).SetEquals(currentIds))
        {
            changes.Add("изменился состав включённых слоёв");
            return;
        }

        if (!previousIds.SequenceEqual(currentIds, StringComparer.Ordinal))
        {
            changes.Add("изменился порядок модов");
        }

        var previousById = previous.ToDictionary(layer => layer.Id, StringComparer.Ordinal);
        foreach (var layer in current)
        {
            if (!previousById.TryGetValue(layer.Id, out var oldLayer))
            {
                continue;
            }

            if (!string.Equals(oldLayer.RootPath, layer.RootPath, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(
                    $"сменился путь слоя «{layer.DisplayName}»: {oldLayer.RootPath} → {layer.RootPath}");
            }
            else if (oldLayer.Order != layer.Order)
            {
                changes.Add($"сменился приоритет слоя «{layer.DisplayName}»: {oldLayer.Order} → {layer.Order}");
            }
        }
    }

    private static void AddSourceChanges(
        IReadOnlyList<WorkspaceBuildSourceFingerprint> previous,
        IReadOnlyList<WorkspaceBuildSourceFingerprint> current,
        List<string> changes)
    {
        var previousSources = previous.ToDictionary(source => source.LayerId, StringComparer.Ordinal);
        foreach (var source in current)
        {
            if (!previousSources.TryGetValue(source.LayerId, out var oldSource))
            {
                continue;
            }

            if (!oldSource.ExcludedFiles.SequenceEqual(source.ExcludedFiles, StringComparer.OrdinalIgnoreCase))
            {
                changes.Add($"изменились исключённые файлы слоя «{source.DisplayName}»");
            }

            AddFileChanges(oldSource, source, changes);
        }
    }

    private static void AddFileChanges(
        WorkspaceBuildSourceFingerprint previous,
        WorkspaceBuildSourceFingerprint current,
        List<string> changes)
    {
        var oldFiles = previous.Files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        var newFiles = current.Files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        foreach (var file in current.Files)
        {
            if (!oldFiles.TryGetValue(file.RelativePath, out var oldFile))
            {
                changes.Add($"добавлен файл слоя «{current.DisplayName}»: {file.RelativePath}");
            }
            else if (oldFile.Length != file.Length ||
                     oldFile.LastWriteTimeUtcTicks != file.LastWriteTimeUtcTicks)
            {
                changes.Add($"изменён файл слоя «{current.DisplayName}»: {file.RelativePath}");
            }
        }

        foreach (var file in previous.Files)
        {
            if (!newFiles.ContainsKey(file.RelativePath))
            {
                changes.Add($"удалён файл слоя «{current.DisplayName}»: {file.RelativePath}");
            }
        }
    }

    private static string DisplayValue(string value, string emptyValue = "не задан") =>
        string.IsNullOrWhiteSpace(value) ? emptyValue : value;
}

internal sealed class WorkspaceBuildManifest
{
    public string Signature { get; set; } = string.Empty;
    public WorkspaceBuildFingerprint? Fingerprint { get; set; }
    public DateTime BuiltAtUtc { get; set; }
    public bool HasStatistics { get; set; }
    public int FileCount { get; set; }
    public int HardLinkCount { get; set; }
    public int SymbolicLinkCount { get; set; }
    public int LocalFileCount { get; set; }
    public long LogicalSizeBytes { get; set; }
    public long PhysicalSizeBytes { get; set; }
}
