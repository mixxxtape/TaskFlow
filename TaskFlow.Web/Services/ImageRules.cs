namespace TaskFlow.Web.Services;

public static class ImageRules
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif" };

    public static bool IsImage(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && Extensions.Contains(Path.GetExtension(fileName));
}
