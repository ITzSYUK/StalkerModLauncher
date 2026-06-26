using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ApProCatalogParserTests
{
    [Fact]
    public void Parse_ExtractsFeaturedEntryDetails()
    {
        const string html = """
            <article class='cCmsCategoryFeaturedEntry ipsClear'>
              <header><h1><a href="/stuff/ten_chernobylja/test-mod-r1/">Тестовый мод</a></h1></header>
              <div class="cCmsRecord_image"><img src="/uploads/test.thumb.jpg"></div>
              <section data-ipsTruncate>Описание <strong>модификации</strong>&nbsp;для теста.</section>
              <ul class='ipsRating_collective'>
                <li class='ipsRating_on'></li><li class='ipsRating_on'></li><li class='ipsRating_half'></li>
              </ul>
              <li class='ipsType_light'>1&nbsp;234 просмотров</li>
            </article>
            """;

        var result = ApProCatalogParser.Parse(html);

        var item = Assert.Single(result);
        Assert.Equal("Тестовый мод", item.Title);
        Assert.Equal("Описание модификации для теста.", item.Description);
        Assert.Equal("https://ap-pro.ru/stuff/ten_chernobylja/test-mod-r1/", item.DetailUrl);
        Assert.Equal("https://ap-pro.ru/uploads/test.thumb.jpg", item.ThumbnailUrl);
        Assert.Equal(2.5, item.Rating);
        Assert.Equal("1 234 просмотров", item.Views);
    }

    [Theory]
    [InlineData(ApProCatalogCategory.ShadowOfChernobyl, "https://ap-pro.ru/stuff/ten_chernobylja/")]
    [InlineData(ApProCatalogCategory.ClearSky, "https://ap-pro.ru/stuff/chistoe_nebo/")]
    [InlineData(ApProCatalogCategory.CallOfPripyat, "https://ap-pro.ru/stuff/zov_pripjati/")]
    public void GetCategoryUrl_ReturnsOfficialCategoryUrl(ApProCatalogCategory category, string expectedUrl)
    {
        Assert.Equal(expectedUrl, ApProCatalogService.GetCategoryUrl(category));
    }

    [Fact]
    public void GetTotalPageCount_ReadsPaginationMetadata()
    {
        const string html = "<ul class='ipsPagination' data-pages='10'><li>...</li></ul>";

        Assert.Equal(10, ApProCatalogParser.GetTotalPageCount(html));
    }

    [Fact]
    public void GetPageUrl_ReturnsExpectedUrlForFollowingPage()
    {
        var url = ApProCatalogService.GetPageUrl(ApProCatalogCategory.ShadowOfChernobyl, 2);

        Assert.Equal("https://ap-pro.ru/stuff/ten_chernobylja/page/2/?d=3", url);
    }
}
