using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StalkerModLauncher.Services;
using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Views;

public partial class ModCatalogWindow : Window
{
    private readonly WindowSystemIntegrationService _windowSystemIntegration;

    public ModCatalogWindow(ModCatalogViewModel viewModel, WindowSystemIntegrationService windowSystemIntegration)
    {
        InitializeComponent();
        DataContext = viewModel;
        _windowSystemIntegration = windowSystemIntegration;
        Loaded += async (_, _) =>
        {
            UpdateCategoryButtons(ApProCatalogCategory.ShadowOfChernobyl);
            await viewModel.LoadInitialAsync(ApProCatalogCategory.ShadowOfChernobyl);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
        base.OnClosed(e);
    }

    private ModCatalogViewModel? ViewModel => DataContext as ModCatalogViewModel;

    private void Window_OnSourceInitialized(object? sender, EventArgs e) => _windowSystemIntegration.Initialize(this);

    private async void ShadowOfChernobylButton_OnClick(object sender, RoutedEventArgs e) => await LoadCategoryAsync(ApProCatalogCategory.ShadowOfChernobyl);

    private async void ClearSkyButton_OnClick(object sender, RoutedEventArgs e) => await LoadCategoryAsync(ApProCatalogCategory.ClearSky);

    private async void CallOfPripyatButton_OnClick(object sender, RoutedEventArgs e) => await LoadCategoryAsync(ApProCatalogCategory.CallOfPripyat);

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            var loadTask = ViewModel.LoadInitialAsync(ViewModel.SelectedCategory, forceRefresh: true);
            CatalogScrollViewer.ScrollToTop();
            await loadTask;
        }
    }

    private void ApProLink_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ViewModel?.OpenWebsite();

    private void ListingButton_OnClick(object sender, RoutedEventArgs e) => ViewModel?.OpenListing((sender as FrameworkElement)?.DataContext as ModCatalogItemViewModel);

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => CatalogScrollViewer.ScrollToTop();

    private void SearchBoxChrome_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!SearchTextBox.IsKeyboardFocusWithin)
        {
            SearchTextBox.Focus();
        }
    }

    private async Task LoadCategoryAsync(ApProCatalogCategory category)
    {
        if (ViewModel is not null && ViewModel.SelectedCategory != category)
        {
            UpdateCategoryButtons(category);
            var loadTask = ViewModel.LoadInitialAsync(category);
            CatalogScrollViewer.ScrollToTop();
            await loadTask;
        }
    }

    private async void CatalogScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 420)
        {
            await (ViewModel?.LoadNextPageAsync() ?? Task.CompletedTask);
        }
    }

    private void UpdateCategoryButtons(ApProCatalogCategory category)
    {
        SetCategoryButtonState(ShadowOfChernobylButton, category == ApProCatalogCategory.ShadowOfChernobyl);
        SetCategoryButtonState(ClearSkyButton, category == ApProCatalogCategory.ClearSky);
        SetCategoryButtonState(CallOfPripyatButton, category == ApProCatalogCategory.CallOfPripyat);
    }

    private void SetCategoryButtonState(Button button, bool isSelected)
    {
        button.Background = (Brush)FindResource(isSelected ? "AccentBrush" : "PanelAltBrush");
        button.Foreground = isSelected
            ? new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0A))
            : (Brush)FindResource("TextBrush");
        button.BorderBrush = (Brush)FindResource(isSelected ? "AccentHoverBrush" : "StrokeBrush");
    }
}
