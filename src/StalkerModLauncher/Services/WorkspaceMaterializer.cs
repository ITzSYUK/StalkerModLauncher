using System.Runtime.InteropServices;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed class WorkspaceMaterializer
{
    public void ValidateLinkSupport(WorkspaceSourceSnapshot snapshot, string workspaceRoot, IProgress<string> progress)
    {
        var workspaceVolume = Path.GetPathRoot(Path.GetFullPath(workspaceRoot));
        var crossVolumeFiles = new[] { snapshot.Game }.Concat(snapshot.Mods.Values)
            .Select(source => source.Files.FirstOrDefault(file => !WorkspaceFileStrategy.MustCopy(file.RelativePath)))
            .Where(file => file is not null)
            .Cast<SourceFileSnapshot>()
            .Where(file => !string.Equals(Path.GetPathRoot(Path.GetFullPath(file.FullPath)), workspaceVolume, StringComparison.OrdinalIgnoreCase))
            .GroupBy(file => Path.GetPathRoot(Path.GetFullPath(file.FullPath)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (crossVolumeFiles.Length == 0)
        {
            return;
        }

        progress.Report("Checking symbolic link support for files stored on other drives...");
        foreach (var sourceFile in crossVolumeFiles)
        {
            var testLink = Path.Combine(workspaceRoot, $".stalker-launcher-link-test-{Guid.NewGuid():N}");
            try
            {
                if (!TryCreateSymbolicFileLink(testLink, sourceFile.FullPath) || !File.Exists(testLink))
                {
                    throw CreateLinkFailureException(sourceFile.FullPath, testLink);
                }
            }
            finally
            {
                File.Delete(testLink);
            }
        }
    }

    public void MirrorBaseGame(
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
            LinkFile(file.FullPath, targetFile, file.RelativePath, stats);

            if (++fileCount % 500 == 0)
            {
                progress.Report($"Linked {fileCount:N0} base files...");
            }
        }
    }

    public void ApplyMod(
        string workspaceRoot,
        ModEntry mod,
        DirectorySnapshot source,
        IProgress<string> progress,
        WorkspaceBuildStats stats,
        CancellationToken cancellationToken)
    {
        progress.Report($"Applying mod: {mod.Name}");
        foreach (var relativePath in source.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(workspaceRoot, relativePath));
        }

        foreach (var file in source.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemSafety.EnsureRelativePath(file.RelativePath, "Mod file");
            var targetFile = Path.Combine(workspaceRoot, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            LinkFile(file.FullPath, targetFile, file.RelativePath, stats);
        }

        progress.Report($"Applied mod '{mod.Name}': {source.Files.Count:N0} files, {source.Directories.Count:N0} folders.");
        if (source.Files.Count == 0)
        {
            progress.Report($"Warning: mod '{mod.Name}' did not contain files visible to the launcher. Check that the profile points to the mod root folder, not an empty wrapper folder.");
        }
    }

    private static void LinkFile(string sourceFile, string targetFile, string relativePath, WorkspaceBuildStats stats)
    {
        var length = new FileInfo(sourceFile).Length;
        if (WorkspaceFileStrategy.MustCopy(relativePath))
        {
            File.Copy(sourceFile, targetFile, overwrite: false);
            stats.Record(relativePath, WorkspaceFileKind.LocalCopy, length);
            return;
        }

        if (TryCreateHardLink(targetFile, sourceFile))
        {
            stats.Record(relativePath, WorkspaceFileKind.HardLink, length);
            return;
        }

        if (TryCreateSymbolicFileLink(targetFile, sourceFile) && File.Exists(targetFile))
        {
            stats.Record(relativePath, WorkspaceFileKind.SymbolicLink, length);
            return;
        }

        File.Delete(targetFile);
        throw CreateLinkFailureException(sourceFile, targetFile);
    }

    private static IOException CreateLinkFailureException(string sourceFile, string targetFile)
    {
        var sourceVolume = GetVolumeDisplayName(sourceFile);
        var workspaceVolume = GetVolumeDisplayName(targetFile);
        var reason = !string.Equals(sourceVolume, workspaceVolume, StringComparison.OrdinalIgnoreCase)
            ? $"Файлы мода находятся на диске {sourceVolume}, а workspace создаётся на диске {workspaceVolume}. Windows не разрешила создать символическую ссылку между ними."
            : $"Windows не разрешила создать ссылку на диске {sourceVolume}. Возможно, диск не использует NTFS или у лаунчера недостаточно прав.";

        return new IOException(
            "Не удалось подключить файлы мода к профилю без копирования." + Environment.NewLine +
            Environment.NewLine + reason + Environment.NewLine + Environment.NewLine +
            "Чтобы исправить проблему:" + Environment.NewLine +
            "1. Включите «Режим разработчика» в параметрах Windows: Система → Для разработчиков." + Environment.NewLine +
            "2. Или запустите лаунчер от имени администратора." + Environment.NewLine +
            $"3. Или перенесите мод на диск {workspaceVolume}." + Environment.NewLine + Environment.NewLine +
            "Сборка остановлена. Файлы игры и мода не изменены.");
    }

    private static string GetVolumeDisplayName(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(root) ? "неизвестный" : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool TryCreateHardLink(string targetFile, string existingFile)
    {
        try { return CreateHardLink(targetFile, existingFile, IntPtr.Zero); }
        catch { return false; }
    }

    private static bool TryCreateSymbolicFileLink(string targetFile, string existingFile)
    {
        try { return CreateSymbolicLink(targetFile, existingFile, 0x2); }
        catch { return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);
}

internal sealed class WorkspaceBuildStats
{
    private readonly Dictionary<string, WorkspaceFileStat> _files = new(StringComparer.OrdinalIgnoreCase);

    public int FileCount => _files.Count;
    public int LinkedFiles => _files.Values.Count(file => file.Kind == WorkspaceFileKind.HardLink);
    public int SymbolicLinkedFiles => _files.Values.Count(file => file.Kind == WorkspaceFileKind.SymbolicLink);
    public int ProtectedCopies => _files.Values.Count(file => file.Kind == WorkspaceFileKind.LocalCopy);
    public long LogicalSizeBytes => _files.Values.Sum(file => file.Length);
    public long PhysicalSizeBytes => _files.Values.Where(file => file.Kind == WorkspaceFileKind.LocalCopy).Sum(file => file.Length);

    public void Record(string relativePath, WorkspaceFileKind kind, long length) =>
        _files[relativePath] = new WorkspaceFileStat(kind, length);
}

internal enum WorkspaceFileKind { HardLink, SymbolicLink, LocalCopy }
internal sealed record WorkspaceFileStat(WorkspaceFileKind Kind, long Length);
