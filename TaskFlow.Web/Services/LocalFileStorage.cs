namespace TaskFlow.Web.Services;

public class LocalFileStorage : IFileStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".txt", ".docx", ".xlsx", ".zip" };

    private readonly string _uploadDir;

    public LocalFileStorage(string webRootPath)
    {
        _uploadDir = Path.Combine(webRootPath, "uploads");
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.Contains(ext))
            throw new InvalidOperationException($"Тип файлу '{ext}' не дозволений.");
        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Файл більший за 5 МБ.");

        Directory.CreateDirectory(_uploadDir);
        var name = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        await using var stream = File.Create(Path.Combine(_uploadDir, name));
        await file.CopyToAsync(stream);
        return UrlPrefix + name;
    }

    public void Delete(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(UrlPrefix)) return;
        var path = Path.Combine(_uploadDir, Path.GetFileName(url));
        if (File.Exists(path)) File.Delete(path);
    }
}
