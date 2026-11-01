using System.Xml.Linq;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

//sitemap.xml має бути валідним XML зі сторінками проєктів
public class SitemapBuilderTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    //перевірка, що sitemap містить головну сторінку та сторінки всіх переданих проєктів
    [Fact]
    public void Build_ProducesValidXml_WithHomeAndEveryProject()
    {
        var xml = SitemapBuilder.Build("https://example.com/", new[]
        {
            (1, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            (7, new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc))
        });

        var doc = XDocument.Parse(xml); // кине виняток, якщо XML невалідний
        var locs = doc.Root!.Elements(Ns + "url").Select(u => u.Element(Ns + "loc")!.Value).ToList();

        Assert.Equal(new[]
        {
            "https://example.com/Projects",
            "https://example.com/Projects/Details/1",
            "https://example.com/Projects/Details/7"
        }, locs);
    }

    //перевірка, що для сторінок проєктів дата останньої зміни записується у форматі ISO, а для головної сторінки відсутня
    [Fact]
    public void Build_WritesLastmodInIsoDate()
    {
        var xml = SitemapBuilder.Build("https://example.com", new[] { (1, new DateTime(2026, 1, 5, 13, 45, 0, DateTimeKind.Utc)) });

        var urls = XDocument.Parse(xml).Root!.Elements(Ns + "url").ToList();

        Assert.Null(urls[0].Element(Ns + "lastmod")); // у головної сторінки дати немає
        Assert.Equal("2026-01-05", urls[1].Element(Ns + "lastmod")!.Value);
    }

    //перевірка, що навіть без проєктів sitemap містить головну сторінку
    [Fact]
    public void Build_NoProjects_StillContainsHomePage()
    {
        var xml = SitemapBuilder.Build("https://example.com", Array.Empty<(int, DateTime)>());
        Assert.Single(XDocument.Parse(xml).Root!.Elements(Ns + "url"));
    }

    //перевірка наявності XML-декларації на початку згенерованого sitemap
    [Fact]
    public void Build_StartsWithXmlDeclaration() =>
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", SitemapBuilder.Build("https://x.com", Array.Empty<(int, DateTime)>()));
}
