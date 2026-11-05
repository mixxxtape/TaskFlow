using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TaskFlow.Web.Api;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

public class ApiCacheTests
{
    private static ApiCache Make() => new(new MemoryCache(new MemoryCacheOptions()));

    //перевірка, що при повторному запиті значення береться з кешу і обчислюється лише один раз
    [Fact]
    public async Task GetOrCreate_SecondCall_UsesCache()
    {
        var cache = Make();
        var calls = 0;

        var a = await cache.GetOrCreateAsync("k", () => Task.FromResult(++calls));
        var b = await cache.GetOrCreateAsync("k", () => Task.FromResult(++calls));

        Assert.Equal(1, a);
        Assert.Equal(1, b);
        Assert.Equal(1, calls);
    }

    //перевірка, що різні ключі кешуються окремо
    [Fact]
    public async Task GetOrCreate_DifferentKeys_AreSeparate()
    {
        var cache = Make();

        var a = await cache.GetOrCreateAsync("a", () => Task.FromResult("A"));
        var b = await cache.GetOrCreateAsync("b", () => Task.FromResult("B"));

        Assert.Equal("A", a);
        Assert.Equal("B", b);
    }

    //перевірка, що після очищення кешу значення обчислюється заново
    [Fact]
    public async Task Invalidate_ForcesRecalculation()
    {
        var cache = Make();
        var calls = 0;
        await cache.GetOrCreateAsync("k", () => Task.FromResult(++calls));

        cache.Invalidate();
        var after = await cache.GetOrCreateAsync("k", () => Task.FromResult(++calls));

        Assert.Equal(2, after);
    }
}

public class TasksApiCachingTests
{
    //перевірка, що API віддає закешовану відповідь, поки дані в базі не скинули кеш
    [Fact]
    public async Task Get_ReturnsCachedValue_UntilInvalidated()
    {
        using var db = new TestDb();
        var project = new Project { Name = "P" };
        var task = new TaskItem { Title = "A", Project = project };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var cache = new ApiCache(new MemoryCache(new MemoryCacheOptions()));
        var controller = new TasksApiController(db.Context, new FakeFileStorage(), null, cache);

        var first = await controller.Get(task.Id);
        task.Title = "B";   
        await db.Context.SaveChangesAsync();
        var cached = await controller.Get(task.Id);
        cache.Invalidate();
        var fresh = await controller.Get(task.Id);

        Assert.Equal("A", first.Value!.Title);
        Assert.Equal("A", cached.Value!.Title); 
        Assert.Equal("B", fresh.Value!.Title);   
    }

    //перевірка, що видалення завдання через API очищає кеш і завдання більше не віддається
    [Fact]
    public async Task Delete_InvalidatesCache()
    {
        using var db = new TestDb();
        var task = new TaskItem { Title = "A", Project = new Project { Name = "P" } };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var cache = new ApiCache(new MemoryCache(new MemoryCacheOptions()));
        var controller = new TasksApiController(db.Context, new FakeFileStorage(), null, cache);
        await controller.Get(task.Id);      
        await controller.Delete(task.Id);
        var result = await controller.Get(task.Id);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}