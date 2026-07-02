using System.ComponentModel;
using System.Diagnostics;

namespace StalkerModLauncher.Services;

public static class ProcessLauncher
{
    private const int ErrorElevationRequired = 740;

    public static Process Start(string executablePath, string? launchArguments, string workingDir)
    {
        try
        {
            return StartCore(executablePath, launchArguments, workingDir, useShellExecute: false, verb: null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorElevationRequired)
        {
            return StartCore(executablePath, launchArguments, workingDir, useShellExecute: true, verb: "runas");
        }
    }

    private static Process StartCore(
        string executablePath,
        string? launchArguments,
        string workingDir,
        bool useShellExecute,
        string? verb)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = launchArguments?.Trim() ?? string.Empty,
            WorkingDirectory = workingDir,
            UseShellExecute = useShellExecute
        };

        if (!string.IsNullOrWhiteSpace(verb))
        {
            startInfo.Verb = verb;
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Windows did not start the game process.");
        }

        return process;
    }
}
