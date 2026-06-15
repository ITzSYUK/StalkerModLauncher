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
    bool StatisticsAvailable = true)
{
    public static WorkspaceStatus Missing(string path) =>
        new(path, false, 0, 0, 0, 0, 0, 0, null, false);

    public string LogicalSizeDisplay => StatisticsAvailable ? FormatSize(LogicalSizeBytes) : "после пересборки";
    public string PhysicalSizeDisplay => StatisticsAvailable ? FormatSize(PhysicalSizeBytes) : "после пересборки";
    public string LinkSummaryDisplay => StatisticsAvailable
        ? $"Hardlink: {HardLinkCount:N0}  ·  Symlink: {SymbolicLinkCount:N0}  ·  Локальные файлы: {LocalFileCount:N0}"
        : "Подробная статистика появится после следующей пересборки workspace.";
    public string BuiltAtDisplay => BuiltAtUtc?.ToLocalTime().ToString("g") ?? "не подготовлен";

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
