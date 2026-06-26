using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace StalkerModLauncher.Services;

public enum ApProCatalogCategory
{
    ShadowOfChernobyl,
    ClearSky,
    CallOfPripyat
}

public sealed record ApProModListing(
    string Title,
    string Description,
    string DetailUrl,
    string? ThumbnailUrl,
    double? Rating,
    string? Views);

public sealed record ApProCatalogPage(
    int PageNumber,
    int TotalPages,
    IReadOnlyList<ApProModListing> Items);

public sealed class ApProCatalogService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);
    private readonly Dictionary<CatalogPageKey, CachedCatalog> _cache = new();

    public static string GetCategoryTitle(ApProCatalogCategory category) => category switch
    {
        ApProCatalogCategory.ShadowOfChernobyl => "Тень Чернобыля",
        ApProCatalogCategory.ClearSky => "Чистое Небо",
        ApProCatalogCategory.CallOfPripyat => "Зов Припяти",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string GetCategoryUrl(ApProCatalogCategory category) => category switch
    {
        ApProCatalogCategory.ShadowOfChernobyl => "https://ap-pro.ru/stuff/ten_chernobylja/",
        ApProCatalogCategory.ClearSky => "https://ap-pro.ru/stuff/chistoe_nebo/",
        ApProCatalogCategory.CallOfPripyat => "https://ap-pro.ru/stuff/zov_pripjati/",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string GetPageUrl(ApProCatalogCategory category, int pageNumber)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        var categoryUrl = GetCategoryUrl(category);
        return pageNumber == 1
            ? $"{categoryUrl}?d=3"
            : $"{categoryUrl}page/{pageNumber}/?d=3";
    }

    public async Task<IReadOnlyList<ApProModListing>> LoadAsync(
        ApProCatalogCategory category,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        return (await LoadPageAsync(category, 1, forceRefresh, cancellationToken)).Items;
    }

    public async Task<ApProCatalogPage> LoadPageAsync(
        ApProCatalogCategory category,
        int pageNumber,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var key = new CatalogPageKey(category, pageNumber);
        if (forceRefresh)
        {
            InvalidateCategory(category);
        }

        if (_cache.TryGetValue(key, out var cached) &&
            DateTimeOffset.UtcNow - cached.LoadedAt < CacheLifetime)
        {
            return cached.Page;
        }

        var html = await HttpClient.GetStringAsync(GetPageUrl(category, pageNumber), cancellationToken);
        if (html.Contains("cf-chl", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Just a moment...", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException("AP-PRO временно запросил проверку браузера.");
        }

        var items = ApProCatalogParser.Parse(html);
        var totalPages = Math.Max(pageNumber, ApProCatalogParser.GetTotalPageCount(html) ?? pageNumber);
        var page = new ApProCatalogPage(pageNumber, totalPages, items);
        _cache[key] = new CachedCatalog(DateTimeOffset.UtcNow, page);
        return page;
    }

    public async Task<byte[]?> DownloadThumbnailAsync(string thumbnailUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetByteArrayAsync(thumbnailUrl, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/136.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.6");
        return client;
    }

    private void InvalidateCategory(ApProCatalogCategory category)
    {
        foreach (var key in _cache.Keys.Where(key => key.Category == category).ToArray())
        {
            _cache.Remove(key);
        }
    }

    private sealed record CatalogPageKey(ApProCatalogCategory Category, int PageNumber);
    private sealed record CachedCatalog(DateTimeOffset LoadedAt, ApProCatalogPage Page);
}

public static class ApProCatalogParser
{
    private static readonly Regex ArticleExpression = new(
        @"<article\b[^>]*\bcCmsCategoryFeaturedEntry\b[^>]*>(?<article>.*?)</article>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex TitleExpression = new(
        "<h1\\b[^>]*>.*?<a\\b[^>]*href=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>(?<title>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex ImageExpression = new(
        "<div\\b[^>]*\\bcCmsRecord_image\\b[^>]*>.*?<img\\b[^>]*src=[\\\"'](?<url>[^\\\"']+)[\\\"']",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex DescriptionExpression = new(
        @"<section\b[^>]*\bdata-ipsTruncate\b[^>]*>(?<description>.*?)</section>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex TagsExpression = new(@"<[^>]+>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceExpression = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex ViewsExpression = new(@"(?<views>[\d\s\u00A0]+просмотров)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PageCountExpression = new("\\bdata-pages=[\\\"'](?<pages>\\d+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int? GetTotalPageCount(string html)
    {
        var match = PageCountExpression.Match(html);
        return match.Success && int.TryParse(match.Groups["pages"].Value, out var pageCount) && pageCount > 0
            ? pageCount
            : null;
    }

    public static IReadOnlyList<ApProModListing> Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<ApProModListing>();
        }

        var result = new List<ApProModListing>();
        foreach (Match articleMatch in ArticleExpression.Matches(html))
        {
            var article = articleMatch.Groups["article"].Value;
            var titleMatch = TitleExpression.Match(article);
            if (!titleMatch.Success)
            {
                continue;
            }

            var detailUrl = ToAbsoluteUrl(titleMatch.Groups["url"].Value);
            if (detailUrl is null)
            {
                continue;
            }

            var imageMatch = ImageExpression.Match(article);
            var descriptionMatch = DescriptionExpression.Match(article);
            var onStars = Regex.Matches(article, @"\bipsRating_on\b", RegexOptions.IgnoreCase).Count;
            var halfStars = Regex.Matches(article, @"\bipsRating_half\b", RegexOptions.IgnoreCase).Count;
            var viewsMatch = ViewsExpression.Match(ToPlainText(article));

            result.Add(new ApProModListing(
                ToPlainText(titleMatch.Groups["title"].Value),
                descriptionMatch.Success ? ToPlainText(descriptionMatch.Groups["description"].Value) : string.Empty,
                detailUrl,
                imageMatch.Success ? ToAbsoluteUrl(imageMatch.Groups["url"].Value) : null,
                onStars + halfStars * 0.5d > 0 ? onStars + halfStars * 0.5d : null,
                viewsMatch.Success ? NormalizeWhitespace(viewsMatch.Groups["views"].Value) : null));
        }

        return result;
    }

    private static string? ToAbsoluteUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }

        return Uri.TryCreate(new Uri("https://ap-pro.ru/"), value, out var relative) ? relative.AbsoluteUri : null;
    }

    private static string ToPlainText(string html) => NormalizeWhitespace(WebUtility.HtmlDecode(TagsExpression.Replace(html, " ")));

    private static string NormalizeWhitespace(string value) => WhitespaceExpression.Replace(value.Replace('\u00A0', ' '), " ").Trim();
}
