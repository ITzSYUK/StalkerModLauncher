using System.Text.Json;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ProfileSettingsValidator _validator = new();

    public void Export(string filePath, ModProfile profile)
    {
        var exported = ToExportedProfile(profile);
        File.WriteAllText(filePath, JsonSerializer.Serialize(exported, JsonOptions));
    }

    public ModProfile Import(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var exported = JsonSerializer.Deserialize<ExportedProfile>(json, JsonOptions)
            ?? throw new InvalidDataException("Файл профиля пуст или имеет неверный формат.");

        var validation = _validator.Validate(exported.Name, exported.ExecutableRelativePath, _ => false);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Messages));
        }

        return ToModProfile(exported);
    }

    internal static ExportedProfile ToExportedProfile(ModProfile profile)
    {
        return new ExportedProfile
        {
            Name = profile.Name,
            IsEnabled = profile.IsEnabled,
            IsStandalone = profile.IsStandalone,
            ExecutableRelativePath = profile.ExecutableRelativePath,
            LaunchArguments = profile.LaunchArguments,
            WorkingDirectoryRelative = profile.WorkingDirectoryRelative,
            GameInstallPath = profile.GameInstallPath,
            ConfigNotes = profile.ConfigNotes,
            Mods = profile.Mods.Select(mod => new ExportedMod
            {
                Name = mod.Name,
                SourcePath = mod.SourcePath,
                IsEnabled = mod.IsEnabled,
                Order = mod.Order,
                Notes = mod.Notes
            }).ToList()
        };
    }

    internal static ModProfile ToModProfile(ExportedProfile exported)
    {
        var profile = new ModProfile
        {
            Name = exported.Name.Trim(),
            IsEnabled = exported.IsEnabled,
            IsStandalone = exported.IsStandalone,
            ExecutableRelativePath = exported.ExecutableRelativePath,
            LaunchArguments = exported.LaunchArguments,
            WorkingDirectoryRelative = exported.WorkingDirectoryRelative,
            GameInstallPath = exported.GameInstallPath,
            ConfigNotes = exported.ConfigNotes
        };

        foreach (var exportedMod in exported.Mods.OrderBy(mod => mod.Order))
        {
            profile.Mods.Add(new ModEntry
            {
                Name = exportedMod.Name,
                SourcePath = exportedMod.SourcePath,
                IsEnabled = exportedMod.IsEnabled,
                Order = profile.Mods.Count + 1,
                Notes = exportedMod.Notes
            });
        }

        return profile;
    }
}
