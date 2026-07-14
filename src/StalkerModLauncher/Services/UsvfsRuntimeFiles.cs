namespace StalkerModLauncher.Services;

public sealed record UsvfsRuntimeFileStatus(
    string Directory,
    string ControllerDllPath,
    string X64ProxyPath,
    string X86DllPath,
    string X86ProxyPath,
    string X86HostPath)
{
    public bool IsX64Ready => File.Exists(ControllerDllPath) && File.Exists(X64ProxyPath);

    public bool IsX86Ready =>
        File.Exists(X86DllPath) &&
        File.Exists(X86HostPath) &&
        File.Exists(ControllerDllPath) &&
        File.Exists(X64ProxyPath);

    public bool IsReady => IsX64Ready;

    public bool IsReadyFor(WindowsExecutableArchitecture architecture)
    {
        return architecture switch
        {
            WindowsExecutableArchitecture.X86 => IsX86Ready,
            WindowsExecutableArchitecture.X64 => IsX64Ready,
            _ => IsX64Ready
        };
    }

    public string MissingFilesMessage(WindowsExecutableArchitecture architecture = WindowsExecutableArchitecture.X64)
    {
        if (IsReadyFor(architecture))
        {
            return string.Empty;
        }

        var requiredFiles = architecture == WindowsExecutableArchitecture.X86
            ? $"{UsvfsRuntimeFiles.X86DllFileName}, {UsvfsRuntimeFiles.X86HostFileName}, " +
              $"{UsvfsRuntimeFiles.ControllerDllFileName} и {UsvfsRuntimeFiles.X64ProxyFileName}"
            : $"{UsvfsRuntimeFiles.ControllerDllFileName} и {UsvfsRuntimeFiles.X64ProxyFileName}";
        var target = architecture == WindowsExecutableArchitecture.X86 ? "32-битной игры" : "64-битной игры";
        return $"Не найден полный комплект USVFS для {target}. " +
               $"Поместите {requiredFiles} рядом с EXE лаунчера: {Directory}";
    }
}

public static class UsvfsRuntimeFiles
{
    public const string ControllerDllFileName = "usvfs_x64.dll";
    public const string X64ProxyFileName = "usvfs_proxy_x64.exe";
    public const string X86DllFileName = "usvfs_x86.dll";
    public const string X86ProxyFileName = "usvfs_proxy_x86.exe";
    public const string X86HostFileName = "StalkerModLauncher.UsvfsX86Host.exe";

    public const string DllFileName = ControllerDllFileName;
    public const string ProxyFileName = X64ProxyFileName;

    public static UsvfsRuntimeFileStatus Check(string? directory = null)
    {
        var root = Path.GetFullPath(directory ?? AppContext.BaseDirectory);
        return new UsvfsRuntimeFileStatus(
            root,
            Path.Combine(root, ControllerDllFileName),
            Path.Combine(root, X64ProxyFileName),
            Path.Combine(root, X86DllFileName),
            Path.Combine(root, X86ProxyFileName),
            Path.Combine(root, X86HostFileName));
    }
}
