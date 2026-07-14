using System.Net;
using System.Net.Http;
using System.Text;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LauncherUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_ReturnsUpdate_WhenLatestReleaseIsNewer()
    {
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(
            """{"tag_name":"v1.2.3","html_url":"https://github.com/ITzSYUK/StalkerModLauncher/releases/tag/v1.2.3"}"""));
        var service = new LauncherUpdateService(handler, "1.2.2");

        var result = await service.CheckAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.2.2", result.CurrentVersion);
        Assert.Equal("v1.2.3", result.LatestVersion);
    }

    [Theory]
    [InlineData("v1.2.2")]
    [InlineData("v1.2.1")]
    public async Task CheckAsync_ReturnsNoUpdate_WhenReleaseIsNotNewer(string tagName)
    {
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(
            $$"""{"tag_name":"{{tagName}}","html_url":"https://github.com/ITzSYUK/StalkerModLauncher/releases/tag/{{tagName}}"}"""));
        var service = new LauncherUpdateService(handler, "1.2.2");

        var result = await service.CheckAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_SendsHonestUserAgent()
    {
        string? userAgent = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            userAgent = request.Headers.UserAgent.ToString();
            return JsonResponse(
                """{"tag_name":"v1.2.2","html_url":"https://github.com/ITzSYUK/StalkerModLauncher/releases/tag/v1.2.2"}""");
        });
        var service = new LauncherUpdateService(handler, "1.2.2");

        await service.CheckAsync();

        Assert.Contains("StalkerModLauncher/1.2.2", userAgent);
        Assert.Contains("github.com/ITzSYUK/StalkerModLauncher", userAgent);
    }

    [Fact]
    public async Task CheckAsync_RejectsUnexpectedReleaseUrl()
    {
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(
            """{"tag_name":"v1.2.3","html_url":"https://example.com/download"}"""));
        var service = new LauncherUpdateService(handler, "1.2.2");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.CheckAsync());
    }

    private static Task<HttpResponseMessage> JsonResponse(string json)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
