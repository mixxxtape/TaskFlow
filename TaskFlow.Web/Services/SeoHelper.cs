using System.Text.RegularExpressions;

namespace TaskFlow.Web.Services;

public static class SeoHelper
{
    public const int MaxDescriptionLength = 160; 

    public static string Description(string? text, string fallback, int max = MaxDescriptionLength)
    {
        var clean = Normalize(text);
        if (clean.Length == 0) clean = Normalize(fallback);
        if (clean.Length <= max) return clean;

        var cut = clean[..(max - 1)]; 
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > max / 2) cut = cut[..lastSpace]; 
        return cut.TrimEnd(' ', ',', '.', ';', ':') + "…";
    }

    public static string Robots(string baseUrl) =>
        "User-agent: *\n" +
        "Allow: /\n" +
        "Disallow: /api/\n" +
        "Disallow: /swagger\n" +
        $"Sitemap: {baseUrl.TrimEnd('/')}/sitemap.xml\n";

    private static string Normalize(string? s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();
}
