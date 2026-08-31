using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public static class ProfileReadinessService
{
    public static ValidationResult Validate(ModProfile? profile)
    {
        if (profile is null)
        {
            return GameInstallationValidator.Validate(string.Empty);
        }

        return profile.IsStandalone
            ? ValidateStandalone(profile)
            : ValidateOverlay(profile);
    }

    private static ValidationResult ValidateStandalone(ModProfile profile)
    {
        var enabledMods = profile.Mods.Where(mod => mod.IsEnabled).ToArray();
        var messages = new List<string>();
        if (!profile.IsEnabled)
        {
            messages.Add("Выбранный профиль отключён.");
        }

        if (enabledMods.Length != 1)
        {
            messages.Add("Автономный профиль должен содержать ровно один включённый мод.");
        }
        else if (!Directory.Exists(enabledMods[0].SourcePath))
        {
            messages.Add($"Папка мода не найдена: {enabledMods[0].Name}");
        }

        var executableIsSafe = ValidateExecutablePath(profile, messages);
        var ready = profile.IsEnabled &&
                    enabledMods.Length == 1 &&
                    Directory.Exists(enabledMods[0].SourcePath) &&
                    executableIsSafe;
        return CreateResult(ready, ready ? "Готов к запуску." : string.Join(Environment.NewLine, messages), messages);
    }

    private static ValidationResult ValidateOverlay(ModProfile profile)
    {
        var gameValidation = GameInstallationValidator.Validate(profile.GameInstallPath);
        var messages = new List<string>(gameValidation.Messages);
        var enabledMods = profile.Mods.Where(mod => mod.IsEnabled).ToArray();
        var missingMods = enabledMods.Where(mod => !Directory.Exists(mod.SourcePath)).ToArray();
        if (!profile.IsEnabled)
        {
            messages.Add("Выбранный профиль отключён.");
        }

        foreach (var mod in missingMods)
        {
            messages.Add($"Папка мода не найдена: {mod.Name}");
        }

        var overwriteExists = string.IsNullOrWhiteSpace(profile.Mo2OverwritePath) ||
                              Directory.Exists(profile.Mo2OverwritePath);
        if (!overwriteExists)
        {
            messages.Add($"Папка MO2 overwrite не найдена: {profile.Mo2OverwritePath}");
        }

        var executableIsSafe = ValidateExecutablePath(profile, messages);
        var exclusionsAreValid = ValidateExcludedFiles(profile, enabledMods, messages);
        var ready = gameValidation.IsValid &&
                    profile.IsEnabled &&
                    missingMods.Length == 0 &&
                    overwriteExists &&
                    exclusionsAreValid &&
                    executableIsSafe;
        return CreateResult(ready, ready ? "Готов к запуску." : string.Join(Environment.NewLine, messages.Distinct()), messages);
    }

    private static bool ValidateExcludedFiles(
        ModProfile profile,
        IReadOnlyList<ModEntry> enabledMods,
        List<string> messages)
    {
        var valid = true;
        var orderedMods = enabledMods.OrderBy(mod => mod.Order).ToArray();
        foreach (var mod in orderedMods)
        {
            foreach (var excluded in mod.ExcludedFiles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    FileSystemSafety.EnsureRelativePath(excluded, "Excluded mod file");
                    var hasOtherProvider = File.Exists(Path.Combine(profile.GameInstallPath, excluded)) ||
                                           orderedMods.Any(other =>
                                               !ReferenceEquals(other, mod) &&
                                               File.Exists(Path.Combine(other.SourcePath, excluded)) &&
                                               !other.ExcludedFiles.Contains(excluded, StringComparer.OrdinalIgnoreCase));
                    if (!hasOtherProvider)
                    {
                        messages.Add($"Исключённый файл больше не имеет другого поставщика: {mod.Name} — {excluded}");
                        valid = false;
                    }
                }
                catch (Exception ex)
                {
                    messages.Add(ex.Message);
                    valid = false;
                }
            }
        }

        return valid;
    }

    private static bool ValidateExecutablePath(ModProfile profile, List<string> messages)
    {
        try
        {
            FileSystemSafety.EnsureRelativePath(profile.ExecutableRelativePath, "Launch executable");
            if (profile.IsStandalone || string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
            {
                return true;
            }

            var pinnedSource = ProfileExecutableSourceResolver.FindPinnedSourceRoot(profile);
            if (pinnedSource is null)
            {
                messages.Add("Ручной источник файла запуска недоступен или мод выключен.");
                return false;
            }

            var executable = FileSystemSafety.ResolvePathInside(
                pinnedSource.RootPath,
                profile.ExecutableRelativePath,
                "Launch executable");
            if (File.Exists(executable))
            {
                return true;
            }

            messages.Add($"Ручной файл запуска не найден: {executable}");
            return false;
        }
        catch (Exception ex)
        {
            messages.Add(ex.Message);
            return false;
        }
    }

    private static ValidationResult CreateResult(bool isValid, string summary, IReadOnlyList<string> messages)
    {
        return new ValidationResult { IsValid = isValid, Summary = summary, Messages = messages };
    }
}
