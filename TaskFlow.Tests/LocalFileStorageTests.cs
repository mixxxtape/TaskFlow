using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "taskflow-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Save_AllowedFile_WritesFileAndReturnsUrl()
    {
        var storage = new LocalFileStorage(_root);

        var url = await storage.SaveAsync(Files.Fake("report.PDF", "content"));

        Assert.StartsWith("/uploads/", url);
        Assert.EndsWith(".pdf", url);
        Assert.True(File.Exists(Path.Combine(_root, "uploads", Path.GetFileName(url))));
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.js")]
    [InlineData("noextension")]
    public async Task Save_ForbiddenExtension_Throws(string name)
    {
        var storage = new LocalFileStorage(_root);

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(Files.Fake(name)));
    }

    [Fact]
    public async Task Delete_RemovesFile_AndIgnoresForeignUrls()
    {
        var storage = new LocalFileStorage(_root);
        var url = await storage.SaveAsync(Files.Fake("a.txt"));

        storage.Delete(url);
        storage.Delete("https://example.com/other.txt");
        storage.Delete(null);

        Assert.False(File.Exists(Path.Combine(_root, "uploads", Path.GetFileName(url))));
    }
}
