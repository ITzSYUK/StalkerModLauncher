namespace StalkerModLauncher.Models;

public sealed class ExportedProfile
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDiscordStatusEnabled { get; set; } = true;
    public bool IsStandalone { get; set; }
    public string ExecutableRelativePath { get; set; } = @"bin\xr_3da.exe";
    public string ExecutableSourcePath { get; set; } = string.Empty;
    public string LaunchArguments { get; set; } = "-nointro";
    public string WorkingDirectoryRelative { get; set; } = string.Empty;
    public string GameInstallPath { get; set; } = string.Empty;
    public List<ExportedMod> Mods { get; set; } = new();
}

public sealed class ExportedMod
{
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Order { get; set; }
}
