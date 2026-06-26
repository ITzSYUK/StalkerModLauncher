using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ModCatalogViewModel : ObservableObject, IDisposable
{
    private readonly ApProCatalogService _catalogService;
    private readonly DialogService _dialogService;
    private CancellationTokenSource? _loadCancellation;
    private ApProCatalogCategory _selectedCategory = ApProCatalogCategory.ShadowOfChernobyl;
    private bool _isLoading;
    private bool _isLoadingMore;
    private bool _hasMorePages;
    private int _nextPageNumber = 1;
    private int _totalPageCount = 1;
    private string _statusText = string.Empty;

    public ModCatalogViewModel(ApProCatalogService catalogService, DialogService dialogService)
    {
        _catalogService = catalogService;
        _dialogService = dialogService;
    }

    public ObservableCollection<ModCatalogItemViewModel> Items { get; } = new();

    public ApProCatalogCategory SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(CategoryTitle));
            }
        }
    }

    public string CategoryTitle => ApProCatalogService.GetCategoryTitle(SelectedCategory);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set => SetProperty(ref _isLoadingMore, value);
    }

    public bool HasMorePages
    {
        get => _hasMorePages;
        private set => SetProperty(ref _hasMorePages, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task LoadInitialAsync(ApProCatalogCategory category, bool forceRefresh = false)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        try
        {
            SelectedCategory = category;
            IsLoading = true;
            StatusText = "Загружаем каталог AP-PRO...";

            DisposeItems();
            _nextPageNumber = 1;
            _totalPageCount = 1;
            HasMorePages = false;

            var page = await _catalogService.LoadPageAsync(category, _nextPageNumber, forceRefresh, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            AddItems(page.Items, cancellationToken);
            _totalPageCount = page.TotalPages;
            _nextPageNumber = page.PageNumber + 1;
            HasMorePages = page.Items.Count > 0 && _nextPageNumber <= _totalPageCount;

            StatusText = Items.Count == 0
                ? "В этом разделе пока не найдено модификаций."
                : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            StatusText = "Не удалось загрузить каталог. Проверьте подключение к интернету и попробуйте обновить страницу.";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    public async Task LoadNextPageAsync()
    {
        if (IsLoading || IsLoadingMore || !HasMorePages || _loadCancellation is null)
        {
            return;
        }

        var cancellationToken = _loadCancellation.Token;
        try
        {
            IsLoadingMore = true;
            var page = await _catalogService.LoadPageAsync(SelectedCategory, _nextPageNumber, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            AddItems(page.Items, cancellationToken);
            _totalPageCount = Math.Max(_totalPageCount, page.TotalPages);
            _nextPageNumber = page.PageNumber + 1;
            HasMorePages = page.Items.Count > 0 && _nextPageNumber <= _totalPageCount;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            StatusText = "Не удалось загрузить следующую страницу. Попробуйте обновить список.";
            HasMorePages = false;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoadingMore = false;
            }
        }
    }

    public void OpenListing(ModCatalogItemViewModel? item)
    {
        if (item is not null)
        {
            _dialogService.OpenUrl(item.DetailUrl);
        }
    }

    public void OpenWebsite() => _dialogService.OpenUrl("https://ap-pro.ru/");

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        DisposeItems();
    }

    private void AddItems(IEnumerable<ApProModListing> listings, CancellationToken cancellationToken)
    {
        var existingUrls = Items.Select(item => item.DetailUrl).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var listing in listings.Where(listing => existingUrls.Add(listing.DetailUrl)))
        {
            var item = new ModCatalogItemViewModel(listing, _catalogService, cancellationToken);
            Items.Add(item);
            item.StartThumbnailLoading();
        }
    }

    private void DisposeItems()
    {
        foreach (var item in Items)
        {
            item.Dispose();
        }

        Items.Clear();
    }
}

public sealed class ModCatalogItemViewModel : ObservableObject, IDisposable
{
    private readonly ApProModListing _listing;
    private readonly ApProCatalogService _catalogService;
    private readonly CancellationToken _parentCancellation;
    private readonly CancellationTokenSource _thumbnailCancellation = new();
    private BitmapImage? _thumbnail;
    private bool _isDisposed;

    public ModCatalogItemViewModel(
        ApProModListing listing,
        ApProCatalogService catalogService,
        CancellationToken parentCancellation)
    {
        _listing = listing;
        _catalogService = catalogService;
        _parentCancellation = parentCancellation;
    }

    public string Title => _listing.Title;
    public string Description => _listing.Description;
    public string DetailUrl => _listing.DetailUrl;
    public string Metadata => string.Join("  ", new[]
    {
        _listing.Rating is null ? null : $"Оценка: {_listing.Rating:0.#} / 10",
        _listing.Views
    }.Where(value => !string.IsNullOrWhiteSpace(value))!);

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    public void StartThumbnailLoading()
    {
        if (string.IsNullOrWhiteSpace(_listing.ThumbnailUrl))
        {
            return;
        }

        _ = LoadThumbnailAsync();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _thumbnailCancellation.Cancel();
        _thumbnailCancellation.Dispose();
        Thumbnail = null;
    }

    private async Task LoadThumbnailAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_parentCancellation, _thumbnailCancellation.Token);
        try
        {
            var bytes = await _catalogService.DownloadThumbnailAsync(_listing.ThumbnailUrl!, cancellation.Token);
            if (bytes is null || _isDisposed)
            {
                return;
            }

            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 360;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Thumbnail = image;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The listing stays usable when its preview is unavailable.
        }
    }
}
