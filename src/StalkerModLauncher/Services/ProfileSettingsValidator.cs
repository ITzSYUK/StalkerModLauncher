using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileSettingsValidator
{
    public ValidationResult Validate(string profileName, string executableRelativePath, Func<string, bool> isNameTaken)
    {
        var messages = new List<string>();
        var normalizedName = profileName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            messages.Add("Укажите название профиля.");
        }
        else if (isNameTaken(normalizedName))
        {
            messages.Add("Профиль с таким именем уже существует.");
        }

        try
        {
            FileSystemSafety.EnsureRelativePath(executableRelativePath, "Файл запуска");
        }
        catch (Exception ex)
        {
            messages.Add(ex.Message);
        }

        return new ValidationResult
        {
            IsValid = messages.Count == 0,
            Summary = messages.Count == 0 ? "Настройки профиля корректны." : messages[0],
            Messages = messages
        };
    }
}
