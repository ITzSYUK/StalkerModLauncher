using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace StalkerModLauncher.Services;

public sealed class OfficialUsvfsNativeApi : IUsvfsNativeApi
{
    private const uint Infinite = 0xFFFFFFFF;

    public IntPtr CreateParameters()
    {
        var parameters = Native.usvfsCreateParameters();
        if (parameters == IntPtr.Zero)
        {
            throw new InvalidOperationException("usvfsCreateParameters returned a null pointer.");
        }

        return parameters;
    }

    public void FreeParameters(IntPtr parameters)
    {
        if (parameters != IntPtr.Zero)
        {
            Native.usvfsFreeParameters(parameters);
        }
    }

    public void SetInstanceName(IntPtr parameters, string name) => Native.usvfsSetInstanceName(parameters, name);
    public void SetDebugMode(IntPtr parameters, bool debugMode) => Native.usvfsSetDebugMode(parameters, debugMode);
    public void SetLogLevel(IntPtr parameters, UsvfsLogLevel level) => Native.usvfsSetLogLevel(parameters, level);
    public void SetCrashDumpType(IntPtr parameters, UsvfsCrashDumpType type) => Native.usvfsSetCrashDumpType(parameters, type);
    public void SetCrashDumpPath(IntPtr parameters, string path) => Native.usvfsSetCrashDumpPath(parameters, path);
    public void InitLogging(bool toLocal) => Native.usvfsInitLogging(toLocal);
    public bool CreateVfs(IntPtr parameters) => Native.usvfsCreateVFS(parameters);
    public void DisconnectVfs() => Native.usvfsDisconnectVFS();
    public void ClearVirtualMappings() => Native.usvfsClearVirtualMappings();

    public bool LinkDirectoryStatic(string sourcePath, string destinationPath, UsvfsLinkFlags flags)
    {
        return Native.usvfsVirtualLinkDirectoryStatic(sourcePath, destinationPath, flags);
    }

    public bool LinkFile(string sourcePath, string destinationPath, UsvfsLinkFlags flags)
    {
        return Native.usvfsVirtualLinkFile(sourcePath, destinationPath, flags);
    }

    public Task<UsvfsProcessHandle> CreateProcessHookedAsync(
        string executablePath,
        string commandLine,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
        if (!Native.usvfsCreateProcessHooked(
                executablePath,
                new StringBuilder(commandLine),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                0,
                IntPtr.Zero,
                workingDirectory,
                ref startup,
                out var processInfo))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "usvfsCreateProcessHooked failed.");
        }

        return Task.FromResult(new UsvfsProcessHandle(
            unchecked((int)processInfo.dwProcessId),
            WaitForExitAsync(processInfo, cancellationToken)));
    }

    private static Task<int> WaitForExitAsync(ProcessInformation processInfo, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var wait = Native.WaitForSingleObject(processInfo.hProcess, 100);
                    if (wait == 0)
                    {
                        break;
                    }

                    if (wait != 0x00000102)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
                    }
                }

                if (!Native.GetExitCodeProcess(processInfo.hProcess, out var exitCode))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed.");
                }

                return unchecked((int)exitCode);
            }
            finally
            {
                Native.CloseHandle(processInfo.hThread);
                Native.CloseHandle(processInfo.hProcess);
            }
        }, cancellationToken);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    private static class Native
    {
        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr usvfsCreateParameters();

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void usvfsFreeParameters(IntPtr parameters);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void usvfsSetInstanceName(IntPtr parameters, string name);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void usvfsSetDebugMode(IntPtr parameters, [MarshalAs(UnmanagedType.Bool)] bool debugMode);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void usvfsSetLogLevel(IntPtr parameters, UsvfsLogLevel level);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void usvfsSetCrashDumpType(IntPtr parameters, UsvfsCrashDumpType type);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void usvfsSetCrashDumpPath(IntPtr parameters, string path);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void usvfsInitLogging([MarshalAs(UnmanagedType.Bool)] bool toLocal);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool usvfsCreateVFS(IntPtr parameters);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void usvfsDisconnectVFS();

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void usvfsClearVirtualMappings();

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool usvfsVirtualLinkDirectoryStatic(
            string source,
            string destination,
            UsvfsLinkFlags flags);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool usvfsVirtualLinkFile(
            string source,
            string destination,
            UsvfsLinkFlags flags);

        [DllImport("usvfs_x64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool usvfsCreateProcessHooked(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}
