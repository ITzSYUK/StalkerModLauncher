using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileVirtualFileDiagnosticsService
{
    private readonly ProfileManager _profileManager;
    private readonly OverlayManifestBuilder _manifestBuilder = new();
    private readonly VirtualFileResolver _resolver = new();
    private readonly OverlayDiagnosticsService _diagnostics = new();

    public ProfileVirtualFileDiagnosticsService(ProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public ProfileVirtualFileInspection InspectLinkedWorkspaceFile(ModProfile profile, string relativePath)
    {
        if (profile.IsStandalone)
        {
            throw new InvalidOperationException("Диагностика виртуального слоя доступна для профилей, которые собирают workspace из базовой игры и модов.");
        }

        if (string.IsNullOrWhiteSpace(profile.GameInstallPath))
        {
            throw new InvalidOperationException("У профиля не указана папка базовой игры.");
        }

        var workspace = _profileManager.GetProfileFolderPath(profile);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            throw new InvalidOperationException("Не удалось определить workspace профиля.");
        }

        var layerPlan = FileLayerPlan.CreateLinkedWorkspace(profile.GameInstallPath, profile, workspace);
        var manifest = _manifestBuilder.BuildLinkedWorkspace(profile, layerPlan, workspace);
        var read = _resolver.ResolveRead(layerPlan, manifest, relativePath);
        var write = _resolver.ResolveWrite(manifest, relativePath);
        var diagnostic = _diagnostics.InspectFile(layerPlan, manifest, relativePath);

        return new ProfileVirtualFileInspection(
            read.RelativePath,
            read.Exists,
            FormatReadSource(read),
            FormatWriteTarget(write),
            FormatProviders(diagnostic.Providers));
    }

    private static string FormatReadSource(VirtualFileResolution resolution)
    {
        if (!resolution.Exists)
        {
            return "Игра не увидит этот файл: он не найден ни в одном слое.";
        }

        return $"Игра увидит файл из слоя «{resolution.SourceName}»: {resolution.PhysicalPath}";
    }

    private static string FormatWriteTarget(VirtualWriteResolution resolution)
    {
        var targetKind = resolution.TargetKind == OverlayWriteTargetKind.KnownWritableFile
            ? "профильный writable-файл"
            : "профильный overwrite";
        return $"Если игра изменит файл, запись пойдёт в {targetKind}: {resolution.PhysicalPath}";
    }

    private static string FormatProviders(IReadOnlyList<OverlayFileProviderSnapshot> providers)
    {
        return providers.Count == 0
            ? "Источников в слоях: 0."
            : $"Источников в слоях: {providers.Count}. Побеждает нижний слой с большим приоритетом.";
    }
}
