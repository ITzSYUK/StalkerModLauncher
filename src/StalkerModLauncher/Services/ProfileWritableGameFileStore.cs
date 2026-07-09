namespace StalkerModLauncher.Services;

internal sealed class ProfileWritableGameFileStore
{
    private const string StoreDirectoryName = "writable-game-files";

    public void CaptureFromWorkspace(string currentWorkspace, string profileWorkspace, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(currentWorkspace))
        {
            return;
        }

        DeleteLegacyStoredFsgame(profileWorkspace, progress);

        var captured = 0;
        foreach (var rule in ProfileWritableGameFiles.Rules)
        {
            var relativePath = rule.RelativePath;
            var workspaceFile = Path.Combine(currentWorkspace, relativePath);
            if (!File.Exists(workspaceFile))
            {
                continue;
            }

            var storedFile = GetStoredFilePath(profileWorkspace, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(storedFile)!);
            CopyThroughTemporaryFile(workspaceFile, storedFile);
            captured++;
        }

        if (captured > 0)
        {
            progress?.Report($"Сохранены профильные игровые настройки из workspace: {captured:N0}.");
        }
    }

    public void EnsureWorkspaceDirectories(string currentWorkspace)
    {
        foreach (var rule in ProfileWritableGameFiles.Rules)
        {
            var relativePath = rule.RelativePath;
            var workspaceFile = Path.Combine(currentWorkspace, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(workspaceFile)!);
        }
    }

    public void RestoreToCachedWorkspace(
        string currentWorkspace,
        string profileWorkspace,
        IProgress<string>? progress = null)
    {
        EnsureWorkspaceDirectories(currentWorkspace);
        DeleteLegacyStoredFsgame(profileWorkspace, progress);

        var restored = 0;
        foreach (var rule in ProfileWritableGameFiles.Rules)
        {
            var relativePath = rule.RelativePath;
            var storedFile = GetStoredFilePath(profileWorkspace, relativePath);
            if (!File.Exists(storedFile))
            {
                continue;
            }

            var workspaceFile = Path.Combine(currentWorkspace, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(workspaceFile)!);
            CopyIndependentWorkspaceFile(storedFile, workspaceFile, relativePath, stats: null);
            restored++;
        }

        if (restored > 0)
        {
            progress?.Report($"Профильные игровые настройки подготовлены к запуску: {restored:N0}.");
        }
    }

    public void RestoreToWorkspace(
        string currentWorkspace,
        string profileWorkspace,
        WorkspaceBuildStats stats,
        IProgress<string>? progress = null)
    {
        EnsureWorkspaceDirectories(currentWorkspace);
        DeleteLegacyStoredFsgame(profileWorkspace, progress);

        var restored = 0;
        foreach (var rule in ProfileWritableGameFiles.Rules)
        {
            var relativePath = rule.RelativePath;
            var workspaceFile = Path.Combine(currentWorkspace, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(workspaceFile)!);

            var storedFile = GetStoredFilePath(profileWorkspace, relativePath);
            if (!File.Exists(storedFile))
            {
                continue;
            }

            CopyIndependentWorkspaceFile(storedFile, workspaceFile, relativePath, stats);
            restored++;
        }

        if (restored > 0)
        {
            progress?.Report($"Восстановлены профильные игровые настройки в workspace: {restored:N0}.");
        }
    }

    private static string GetStoredFilePath(string profileWorkspace, string relativePath)
    {
        FileSystemSafety.EnsureRelativePath(relativePath, "Profile writable game file");
        return Path.Combine(profileWorkspace, "userdata", StoreDirectoryName, relativePath);
    }

    private static void DeleteLegacyStoredFsgame(string profileWorkspace, IProgress<string>? progress)
    {
        var legacyPath = Path.Combine(profileWorkspace, "userdata", StoreDirectoryName, "fsgame.ltx");
        if (!File.Exists(legacyPath))
        {
            return;
        }

        File.Delete(legacyPath);
        progress?.Report("Удалён устаревший профильный fsgame.ltx. Лаунчер пересоздаёт этот файл из текущих слоёв профиля.");
    }

    private static void CopyThroughTemporaryFile(string sourceFile, string destinationFile)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationFile)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryFile = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationFile)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourceFile, temporaryFile, overwrite: false);
            ClearReadOnlyAttribute(temporaryFile);
            File.Move(temporaryFile, destinationFile, overwrite: true);
            ClearReadOnlyAttribute(destinationFile);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static void CopyIndependentWorkspaceFile(
        string sourceFile,
        string workspaceFile,
        string relativePath,
        WorkspaceBuildStats? stats)
    {
        if (File.Exists(workspaceFile))
        {
            File.Delete(workspaceFile);
        }

        File.Copy(sourceFile, workspaceFile, overwrite: false);
        ClearReadOnlyAttribute(workspaceFile);
        stats?.RecordRequiredLocal(relativePath, new FileInfo(workspaceFile).Length);
    }

    private static void ClearReadOnlyAttribute(string filePath)
    {
        var attributes = File.GetAttributes(filePath);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
