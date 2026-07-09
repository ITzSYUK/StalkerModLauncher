namespace StalkerModLauncher.Services;

public sealed record UsvfsRuntimeFileStatus(
    string Directory,
    string DllPath,
    string ProxyPath,
    bool IsReady)
{
    public string MissingFilesMessage()
    {
        if (IsReady)
        {
            return string.Empty;
        }

        return $"USVFS runtime files were not found. Put usvfs_x64.dll and usvfs_proxy_x64.exe next to the launcher executable: {Directory}";
    }
}

public static class UsvfsRuntimeFiles
{
    public const string DllFileName = "usvfs_x64.dll";
    public const string ProxyFileName = "usvfs_proxy_x64.exe";

    public static UsvfsRuntimeFileStatus Check(string? directory = null)
    {
        var root = Path.GetFullPath(directory ?? AppContext.BaseDirectory);
        var dll = Path.Combine(root, DllFileName);
        var proxy = Path.Combine(root, ProxyFileName);
        return new UsvfsRuntimeFileStatus(
            root,
            dll,
            proxy,
            File.Exists(dll) && File.Exists(proxy));
    }
}
