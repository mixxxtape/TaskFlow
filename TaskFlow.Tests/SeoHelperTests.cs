using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

//юніт-тести генерації meta description і robots.txt
public class SeoHelperTests
{
    //перевірка, що короткий опис повертається без змін
    [Fact]
    public void Description_ShortText_IsReturnedAsIs() =>
        Assert.Equal("Короткий опис", SeoHelper.Description("Короткий опис", "fallback"));

    //перевірка, що для порожнього або відсутнього опису використовується запасний текст
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Description_EmptyText_UsesFallback(string? text) =>
        Assert.Equal("fallback text", SeoHelper.Description(text, "fallback text"));

    //перевірка видалення зайвих пробілів, табуляцій та переносів рядків
    [Fact]
    public void Description_CollapsesWhitespaceAndNewlines() =>
        Assert.Equal("a b c", SeoHelper.Description("  a \n\n b\t c ", "x"));

    //перевірка обрізання довгого опису до встановленої довжини без розриву слів
    [Fact]
    public void Description_LongText_IsCutAtWordBoundaryWithEllipsis()
    {
        var text = string.Join(" ", Enumerable.Repeat("слово", 100));

        var result = SeoHelper.Description(text, "x");

        Assert.True(result.Length <= SeoHelper.MaxDescriptionLength);
        Assert.EndsWith("…", result);
        Assert.Matches(@"^(слово )*слово…$", result); // жодне слово не обрізане посередині
    }

    //перевірка, що опис максимально допустимої довжини повертається без обрізання
    [Fact]
    public void Description_TextExactlyAtLimit_IsNotTruncated()
    {
        var text = new string('a', SeoHelper.MaxDescriptionLength);
        Assert.Equal(text, SeoHelper.Description(text, "x"));
    }

    //перевірка, що навіть довге слово без пробілів не перевищує встановлену довжину
    [Fact]
    public void Description_LongTextWithoutSpaces_IsStillWithinLimit()
    {
        var result = SeoHelper.Description(new string('a', 500), "x");
        Assert.Equal(SeoHelper.MaxDescriptionLength, result.Length);
        Assert.EndsWith("…", result);
    }

    //перевірка, що robots.txt дозволяє індексацію сайту, приховує API та містить адресу sitemap.xml
    [Fact]
    public void Robots_AllowsSite_HidesApi_AndPointsToSitemap()
    {
        var robots = SeoHelper.Robots("https://example.com/");

        Assert.Contains("User-agent: *", robots);
        Assert.Contains("Disallow: /api/", robots);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", robots);
    }
}
