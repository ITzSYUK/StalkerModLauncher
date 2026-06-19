namespace StalkerModLauncher.Models;

public sealed record WorkspaceStatus(
    string WorkspacePath,
    bool Exists,
    long LogicalSizeBytes,
    long PhysicalSizeBytes,
    int FileCount,
    int SymbolicLinkCount,
    int HardLinkCount,
    int LocalFileCount,
    DateTime? BuiltAtUtc,
    bool StatisticsAvailable = true,
    bool RootExists = false,
    bool CurrentExists = false,
    bool ManifestExists = false)
{
    public static WorkspaceStatus Missing(string path) =>
        new(path, false, 0, 0, 0, 0, 0, 0, null, false, Directory.Exists(path), false, false);

    public string WorkspacePathDisplay => string.IsNullOrWhiteSpace(WorkspacePath)
        ? "Путь будет назначен при первой сборке workspace."
        : WorkspacePath;
    public string CurrentPathDisplay => string.IsNullOrWhiteSpace(WorkspacePath)
        ? "Папка current будет создана при запуске."
        : Path.Combine(WorkspacePath, "current");
    public string UserDataPathDisplay => string.IsNullOrWhiteSpace(WorkspacePath)
        ? "Папка userdata появится рядом с workspace."
        : Path.Combine(WorkspacePath, "userdata");
    public string LogicalSizeDisplay => StatisticsAvailable ? FormatSize(LogicalSizeBytes) : "после пересборки";
    public string PhysicalSizeDisplay => StatisticsAvailable ? FormatSize(PhysicalSizeBytes) : "после пересборки";
    public string FileCountDisplay => StatisticsAvailable ? $"{FileCount:N0}" : "после пересборки";
    public string LinkSummaryDisplay => StatisticsAvailable
        ? $"Hardlink: {HardLinkCount:N0}  ·  Symlink: {SymbolicLinkCount:N0}  ·  свои файлы: {LocalFileCount:N0}"
        : "Подробная статистика появится после следующей пересборки workspace.";
    public string BuiltAtDisplay => BuiltAtUtc?.ToLocalTime().ToString("g") ?? "не подготовлен";
    public string StateDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WorkspacePath))
            {
                return "Рабочая папка ещё не выбрана.";
            }

            if (!RootExists)
            {
                return "Рабочая папка ещё не создана.";
            }

            if (!CurrentExists)
            {
                return "Файлы для запуска ещё не подготовлены.";
            }

            return ManifestExists
                ? "Рабочая папка готова."
                : "Рабочая папка есть, но её лучше пересобрать.";
        }
    }

    public string SizeExplanationDisplay => StatisticsAvailable
        ? "Логический размер показывает, сколько данных видит игра. Реально занимает — примерная нагрузка на диск с учётом ссылок."
        : "Точная статистика появится после следующей сборки workspace.";

    public static string FormatSize(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:N1} {units[unit]}";
    }
}
