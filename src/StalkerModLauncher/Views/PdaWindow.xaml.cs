using System.Windows;
using System.Windows.Input;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;
using StalkerModLauncher.Views.Controls;

namespace StalkerModLauncher.Views;

public partial class PdaWindow : Window
{
    private readonly WindowNavigationService _navigation;

    public PdaWindow(MainViewModel viewModel, WindowNavigationService navigation)
    {
        InitializeComponent();
        _navigation = navigation;
        DataContext = viewModel;
        viewModel.ProfileCreationRequested += ViewModel_ProfileCreationRequested;
        viewModel.ModScanSelectionRequested += ViewModel_ModScanSelectionRequested;
        Closed += (_, _) =>
        {
            viewModel.ProfileCreationRequested -= ViewModel_ProfileCreationRequested;
            viewModel.ModScanSelectionRequested -= ViewModel_ModScanSelectionRequested;
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void ViewModel_ProfileCreationRequested(object? sender, EventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var creationViewModel = _navigation.CreateProfileCreationViewModel();
        var page = new PdaProfileCreationView { DataContext = creationViewModel };
        creationViewModel.Completed += (_, profile) =>
        {
            ViewModel.AddCreatedProfile(profile);
            PdaView.ShowProfilePage();
        };
        page.Cancelled += (_, _) => PdaView.ShowProfilePage();
        PdaView.ShowPage(page, "Создание профиля");
    }

    private void EditProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settingsVm = ViewModel?.CreateProfileSettingsViewModel();
        if (settingsVm is not null)
        {
            var page = new PdaProfileSettingsView { DataContext = settingsVm };
            page.Saved += (_, _) => PdaView.ShowProfilePage();
            PdaView.ShowPage(page, $"Настройки: {settingsVm.ProfileName}", showProfileTypeIcon: true);
        }
    }

    private void ViewModel_ModScanSelectionRequested(object? sender, ModScanSelectionRequest request)
    {
        var page = new PdaScanResultsView(request.Mods);
        page.Accepted += (_, _) =>
        {
            request.Accept(page.GetSelectedMods());
            PdaView.ShowProfilePage();
        };
        page.Cancelled += (_, _) =>
        {
            request.Cancel();
            PdaView.ShowProfilePage();
        };
        PdaView.ShowPage(page, "Найденные моды", lifetime: page, showProfileTypeIcon: true);
    }

    private void ScreenshotsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedProfile is { } profile)
        {
            var screenshotsVm = _navigation.CreateScreenshotsViewModel(profile);
            PdaView.ShowPage(
                new PdaScreenshotsView { DataContext = screenshotsVm },
                $"Скриншоты: {profile.Name}",
                lifetime: screenshotsVm,
                showProfileTypeIcon: true);
        }
    }

    private void ModCatalogButton_OnClick(object sender, RoutedEventArgs e)
    {
        var catalogVm = _navigation.CreateModCatalogViewModel();
        PdaView.ShowPage(
            new PdaModCatalogView { DataContext = catalogVm },
            "Каталог модификаций",
            "AP-PRO.RU",
            catalogVm);
    }

    private void ProfileHealthButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedProfile is { } profile)
        {
            var healthVm = _navigation.CreateProfileHealthViewModel(profile, ViewModel.AppendLog);
            PdaView.ShowPage(
                new PdaHealthView { DataContext = healthVm },
                $"Состояние: {profile.Name}",
                healthVm.ProfileKind,
                healthVm,
                showProfileTypeIcon: true);
        }
    }

    private void AboutButton_OnClick(object sender, RoutedEventArgs e)
    {
        PdaView.ShowPage(new PdaAboutView(_navigation), "О программе");
    }

    private void LogButton_OnClick(object sender, RoutedEventArgs e)
    {
        PdaView.ShowPage(new PdaLogView { DataContext = ViewModel }, "Журнал лаунчера");
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(this).Y < 94)
        {
            DragMove();
        }
    }
}
