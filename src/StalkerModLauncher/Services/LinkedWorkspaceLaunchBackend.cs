using System.Diagnostics;
using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class LinkedWorkspaceLaunchBackend : IProfileLaunchBackend
{
    private const string AutoLoadBeginMarker = "-- STALKER_MOD_LAUNCHER_AUTOLOAD_BEGIN";
    private const string AutoLoadEndMarker = "-- STALKER_MOD_LAUNCHER_AUTOLOAD_END";
    private readonly WorkspaceBuilder _workspaceBuilder;

    public LinkedWorkspaceLaunchBackend(WorkspaceBuilder workspaceBuilder)
    {
        _workspaceBuilder = workspaceBuilder;
    }

    public LaunchBackendKind Kind => LaunchBackendKind.LinkedWorkspace;

    public async Task<Process> LaunchAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceBuilder.BuildAsync(gamePath, profile, progress, cancellationToken);
        profile.WorkspacePath = workspace.ProfileWorkspacePath;
        profile.ExecutableRelativePath = workspace.ExecutableRelativePath;
        profile.WorkingDirectoryRelative = workspace.WorkingDirectoryRelative;
        RemoveLegacyScriptAutoloadPatch(workspace, profile, progress);
        progress.Report($"Starting: {workspace.ExecutablePath}");

        var workingDir = string.IsNullOrWhiteSpace(profile.WorkingDirectoryRelative)
            ? workspace.WorkspaceRoot
            : FileSystemSafety.ResolvePathInside(
                workspace.WorkspaceRoot,
                profile.WorkingDirectoryRelative,
                "Working directory");

        return ProcessLauncher.Start(workspace.ExecutablePath, profile.LaunchArguments, workingDir);
    }

    private static void RemoveLegacyScriptAutoloadPatch(WorkspaceBuildResult workspace, ModProfile profile, IProgress<string> progress)
    {
        if (profile.IsStandalone)
        {
            return;
        }

        var scriptRoot = string.IsNullOrWhiteSpace(profile.WorkingDirectoryRelative)
            ? workspace.WorkspaceRoot
            : Path.Combine(workspace.WorkspaceRoot, profile.WorkingDirectoryRelative);
        var scriptPath = Path.Combine(scriptRoot, "gamedata", "scripts", "ui_main_menu.script");
        if (!File.Exists(scriptPath))
        {
            return;
        }

        var text = File.ReadAllText(scriptPath, Encoding.Default);
        var cleanedText = RemoveAutoloadBlock(text);

        if (cleanedText == text)
        {
            return;
        }

        // This file can be linked from the source mod. Delete the workspace entry first so
        // the cleanup never writes back into the mod folder.
        File.Delete(scriptPath);
        File.WriteAllText(scriptPath, cleanedText, Encoding.Default);
        progress.Report("Removed legacy save autoload hook from profile workspace.");
    }

    private static string RemoveAutoloadBlock(string text)
    {
        while (true)
        {
            var begin = text.IndexOf(AutoLoadBeginMarker, StringComparison.Ordinal);
            if (begin < 0)
            {
                return text;
            }

            var end = text.IndexOf(AutoLoadEndMarker, begin, StringComparison.Ordinal);
            if (end < 0)
            {
                return text[..begin];
            }

            end += AutoLoadEndMarker.Length;
            text = text.Remove(begin, end - begin);
        }
    }
}
