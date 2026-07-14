using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace StalkerModLauncher.Services;

public sealed record LauncherUpdateResult(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    bool IsUpdateAvailable);

public sealed class LauncherUpdateService
{
    public const string LatestReleasePageUrl =
        "https://github.com/ITzSYUK/StalkerModLauncher/releases/latest";

    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/ITzSYUK/StalkerModLauncher/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;

    public LauncherUpdateService(
        HttpMessageHandler? httpMessageHandler = null,
        string? currentVersion = null)
    {
        _currentVersion = currentVersion ?? GetApplicationVersion();
        _httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"StalkerModLauncher/{_currentVersion} (+https://github.com/ITzSYUK/StalkerModLauncher)");
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<LauncherUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tagName = GetRequiredString(root, "tag_name");
        var releaseUrl = GetRequiredString(root, "html_url");
        ValidateReleaseUrl(releaseUrl);

        var current = ParseVersion(_currentVersion, "current application version");
        var latest = ParseVersion(tagName, "latest GitHub release tag");
        return new LauncherUpdateResult(
            _currentVersion,
            tagName.Trim(),
            releaseUrl,
            latest > current);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"GitHub response does not contain '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static void ValidateReleaseUrl(string releaseUrl)
    {
        if (!Uri.TryCreate(releaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(
                "/ITzSYUK/StalkerModLauncher/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("GitHub returned an unexpected release URL.");
        }
    }

    private static Version ParseVersion(string value, string description)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        if (!Version.TryParse(normalized, out var version))
        {
            throw new InvalidDataException($"Invalid {description}: {value}");
        }

        return new Version(
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(LauncherUpdateService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return informationalVersion?.Split('+')[0]
               ?? assembly.GetName().Version?.ToString(3)
               ?? "0.0.0";
    }
}
