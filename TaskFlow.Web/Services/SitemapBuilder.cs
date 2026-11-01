using System.Xml.Linq;

namespace TaskFlow.Web.Services;

public static class SitemapBuilder
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static string Build(string baseUrl, IEnumerable<(int Id, DateTime CreatedAt)> projects)
    {
        baseUrl = baseUrl.TrimEnd('/');

        var urls = new List<XElement> { Url($"{baseUrl}/Projects", null, "daily") };
        foreach (var p in projects)
            urls.Add(Url($"{baseUrl}/Projects/Details/{p.Id}", p.CreatedAt, "weekly"));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Ns + "urlset", urls));
        return doc.Declaration + "\n" + doc.Root;
    }

    private static XElement Url(string loc, DateTime? lastMod, string changeFreq) =>
        new(Ns + "url",
            new XElement(Ns + "loc", loc),
            lastMod is null ? null : new XElement(Ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd")),
            new XElement(Ns + "changefreq", changeFreq));
}
