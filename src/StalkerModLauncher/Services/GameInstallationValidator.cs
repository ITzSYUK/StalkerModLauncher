using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class GameInstallationValidator
{
    private static readonly string[] KnownExecutableCandidates =
    {
        @"bin\xr_3da.exe",
        "xr_3da.exe",
        @"bin\xrEngine.exe",
        @"bin_x64\xrEngine.exe",
        @"bin_x64\OGSR_Engine.exe",
        "AnomalyLauncher.exe",
        @"bin\AnomalyDX11.exe",
        @"bin\AnomalyDX9.exe",
        @"bin\AnomalyDX9_AVX.exe"
    };

    public ValidationResult Validate(string? gamePath)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return Invalid("Папка игры не выбрана.", "Выберите папку с установленной игрой.");
        }

        if (!Directory.Exists(gamePath))
        {
            return Invalid("Папка игры не существует.", gamePath);
        }

        var hasFsgame = File.Exists(Path.Combine(gamePath, "fsgame.ltx"));
        var executable = KnownExecutableCandidates.FirstOrDefault(candidate => File.Exists(Path.Combine(gamePath, candidate)));

        if (executable is not null)
        {
            messages.Add($"Найден исполняемый файл: {executable}");
        }
        else if (!hasFsgame)
        {
            messages.Add("Известный исполняемый файл игры не найден.");
        }
        else
        {
            messages.Add("Известный исполняемый файл не найден, но fsgame.ltx присутствует.");
        }

        if (!hasFsgame && executable is not null)
        {
            messages.Add("fsgame.ltx не найден. Лаунчер может работать некорректно с этой папкой.");
        }
        else if (!hasFsgame)
        {
            messages.Add("fsgame.ltx не найден. Похоже, это не корневая папка S.T.A.L.K.E.R.");
        }

        var isValid = executable is not null;
        return new ValidationResult
        {
            IsValid = isValid,
            Summary = isValid ? "Папка игры в порядке." : "Папка игры не настроена.",
            Messages = messages
        };

        static ValidationResult Invalid(string summary, string detail)
        {
            return new ValidationResult
            {
                IsValid = false,
                Summary = summary,
                Messages = new[] { detail }
            };
        }
    }
}
