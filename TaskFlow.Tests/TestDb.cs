using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

/// <summary>Окрема SQLite in-memory база на кожен тест: швидко й без Docker.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

public class FakeFileStorage : IFileStorage
{
    public List<string> Saved { get; } = new();
    public List<string?> Deleted { get; } = new();

    public Task<string> SaveAsync(IFormFile file)
    {
        var url = "/uploads/fake-" + file.FileName;
        Saved.Add(url);
        return Task.FromResult(url);
    }

    public void Delete(string? url) => Deleted.Add(url);
}

public static class Files
{
    public static IFormFile Fake(string name = "doc.pdf", string content = "hello")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Attachment", name);
    }
}
