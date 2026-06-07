namespace StalkerModLauncher.Models;

public sealed class AppSettings
{
    public string GameInstallPath { get; set; } = string.Empty;
    public List<ModProfile> Profiles { get; set; } = new();
    public bool DontShowAboutOnStartup { get; set; }
    public bool IsLogVisible { get; set; } = true;
    public string DiscordClientId { get; set; } = "1510923765431799898";
}
