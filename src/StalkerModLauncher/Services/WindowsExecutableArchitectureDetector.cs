using System.Reflection.PortableExecutable;

namespace StalkerModLauncher.Services;

public enum WindowsExecutableArchitecture
{
    Unknown,
    X86,
    X64
}

public static class WindowsExecutableArchitectureDetector
{
    public static WindowsExecutableArchitecture Detect(string executablePath)
    {
        try
        {
            using var stream = File.OpenRead(executablePath);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.CoffHeader.Machine switch
            {
                Machine.I386 => WindowsExecutableArchitecture.X86,
                Machine.Amd64 => WindowsExecutableArchitecture.X64,
                _ => WindowsExecutableArchitecture.Unknown
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return WindowsExecutableArchitecture.Unknown;
        }
    }
}
