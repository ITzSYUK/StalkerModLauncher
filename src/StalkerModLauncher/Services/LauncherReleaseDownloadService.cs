using System.Net.Http;
using System.Net.Http.Headers;

namespace StalkerModLauncher.Services;

public enum LauncherReleasePackage
{
    Minimal,
    Standalone
}

public static class LauncherReleaseDownloadService
{
    private const string GitHubRepository = "ITzSYUK/CORDON";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<string> DownloadAsync(
        string releaseUrl,
        string releaseTag,
        LauncherReleasePackage package,
        CancellationToken cancellationToken = default)
    {
        var downloadUri = BuildDownloadUri(releaseUrl, releaseTag, package);
        var destinationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(destinationDirectory);

        var destinationPath = GetAvailablePath(destinationDirectory, Path.GetFileName(downloadUri.LocalPath));
        var temporaryPath = destinationPath + ".partial";

        try
        {
            using var response = await HttpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath);
            return destinationPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static Uri BuildDownloadUri(
        string releaseUrl,
        string releaseTag,
        LauncherReleasePackage package)
    {
        if (!Uri.TryCreate(releaseUrl, UriKind.Absolute, out var releaseUri) ||
            releaseUri.Scheme != Uri.UriSchemeHttps ||
            !releaseUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !releaseUri.AbsolutePath.Equals(
                $"/{GitHubRepository}/releases/tag/{releaseTag}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("GitHub returned an unexpected release URL.");
        }

        var fileName = package switch
        {
            LauncherReleasePackage.Minimal => $"CORDON-{releaseTag}-win-x64.zip",
            LauncherReleasePackage.Standalone => $"CORDON-{releaseTag}-win-x64-standalone.zip",
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null)
        };

        return new Uri($"https://github.com/{GitHubRepository}/releases/download/{releaseTag}/{fileName}");
    }

    private static string GetAvailablePath(string directory, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, fileName);
        var number = 1;

        while (File.Exists(candidate) || File.Exists(candidate + ".partial"))
        {
            candidate = Path.Combine(directory, $"{name} ({number++}){extension}");
        }

        return candidate;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CORDON release downloader");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}
