using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;


//cтворення окремої тестової бази даних SQLite в пам'яті, щоб тести не змінювали основну базу даних проєкту
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

    //закриття контексту і з'єднання з тестовою базою після завершення тесту
    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

//імітація роботи з файлами, щоб у тестах не створювати справжні вкладення
public class FakeFileStorage : IFileStorage
{
    public List<string> Saved { get; } = new();
    public List<string?> Deleted { get; } = new();

    //імітація збереження файлу та повернення тестового посилання на нього
    public Task<string> SaveAsync(IFormFile file)
    {
        var url = "/uploads/fake-" + file.FileName;
        Saved.Add(url);
        return Task.FromResult(url);
    }

    //запис посилання на файл, який контролер попросив видалити
    public void Delete(string? url) => Deleted.Add(url);
}

public static class Files
{
    //створення тестового файлу, який можна передавати в методи контролерів
    public static IFormFile Fake(string name = "doc.pdf", string content = "hello")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Attachment", name);
    }
}
