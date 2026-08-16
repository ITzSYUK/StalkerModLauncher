using System.Diagnostics;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public enum GameLaunchReadinessStatus
{
    Ready,
    Stalled,
    ExitedBeforeReady
}

public sealed record GameLaunchReadinessResult(
    GameLaunchReadinessStatus Status,
    string Details,
    IReadOnlyList<int> ProcessIds);

public sealed record GameProcessReadinessState(
    bool HasMainWindow,
    long WorkingSetBytes,
    bool IsInfrastructureProcess = false);

public sealed class GameLaunchReadinessMonitor
{
    private const long ReadyWorkingSetBytes = 32L * 1024 * 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _timeout;

    public GameLaunchReadinessMonitor(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<GameLaunchReadinessResult> MonitorAsync(
        ProfileLaunchHandle launch,
        ModProfile profile,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var deadline = startedAtUtc + _timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processIds = launch.GetActiveProcessIds();
            var processStates = ReadProcessStates(processIds);
            var freshLog = FindFreshGameLog(profile, startedAtUtc);
            var signal = EvaluateReadySignal(processStates, freshLog);
            if (signal is not null)
            {
                return new GameLaunchReadinessResult(
                    GameLaunchReadinessStatus.Ready,
                    signal,
                    processIds);
            }

            if (HasCompleted(launch))
            {
                return new GameLaunchReadinessResult(
                    GameLaunchReadinessStatus.ExitedBeforeReady,
                    "Процесс завершился до появления окна, игрового лога или нормальной загрузки памяти.",
                    processIds);
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return new GameLaunchReadinessResult(
            GameLaunchReadinessStatus.Stalled,
            "За 30 секунд не появилось окно игры, свежий игровой лог или процесс с нормальной загрузкой памяти.",
            launch.GetActiveProcessIds());
    }

    public static string? EvaluateReadySignal(
        IEnumerable<GameProcessReadinessState> processStates,
        string? freshLogPath)
    {
        var gameProcesses = processStates.Where(state => !state.IsInfrastructureProcess).ToArray();
        if (gameProcesses.Any(state => state.HasMainWindow))
        {
            return "обнаружено окно игры";
        }

        if (gameProcesses.Any(state => state.WorkingSetBytes >= ReadyWorkingSetBytes))
        {
            return "процесс движка начал нормальную загрузку";
        }

        return freshLogPath is null ? null : $"обновлён игровой лог: {freshLogPath}";
    }

    private static bool HasCompleted(ProfileLaunchHandle launch)
    {
        if (launch.Completion is not null)
        {
            return launch.Completion.IsCompleted;
        }

        try
        {
            return launch.Process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static List<GameProcessReadinessState> ReadProcessStates(IEnumerable<int> processIds)
    {
        var result = new List<GameProcessReadinessState>();
        foreach (var processId in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Refresh();
                result.Add(new GameProcessReadinessState(
                    process.MainWindowHandle != IntPtr.Zero,
                    process.WorkingSet64,
                    process.ProcessName.Equals(
                        "StalkerModLauncher.UsvfsX86Host",
                        StringComparison.OrdinalIgnoreCase)));
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        return result;
    }

    private static string? FindFreshGameLog(ModProfile profile, DateTime startedAtUtc)
    {
        try
        {
            return ProfileDataPathResolver.GetLogDirectories(profile)
                .Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                .Select(path => new FileInfo(path))
                .Where(file => file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                               file.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Name.StartsWith("usvfs", StringComparison.OrdinalIgnoreCase))
                .Where(file => file.LastWriteTimeUtc >= startedAtUtc)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
