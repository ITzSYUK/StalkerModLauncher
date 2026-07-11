using System.Text.Json.Serialization;

namespace StalkerModLauncher.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string LastBrowsedGamePath { get; set; } = string.Empty;

    [JsonPropertyName("GameInstallPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyGameInstallPath { get; set; }

    public List<ModProfile> Profiles { get; set; } = new();
    public bool DontShowAboutOnStartup { get; set; }
    public bool IsLogVisible { get; set; } = true;
    public string DiscordClientId { get; set; } = "1510923765431799898";
}
