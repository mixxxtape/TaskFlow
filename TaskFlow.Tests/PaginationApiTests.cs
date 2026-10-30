using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

//тести перевірки пагінації REST API проєктів
public class PaginationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaginationApiTests(CustomWebApplicationFactory f)
    {
        _factory = f;
        _client = f.CreateClient();
    }

    // додає тестові проєкти до бази даних
    private void Seed(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Projects.Any()) return;

        for (var i = 1; i <= count; i++)
            db.Projects.Add(new Project { Name = $"P{i:D2}" });

        db.SaveChanges();
    }

    //перевіряє перехід між сторінками та відсутність повторів елементів
    [Fact]
    public async Task FollowingNextLink_ReturnsAllItemsOnce_AndLastPageHasNoNextLink()
    {
        Seed(25);

        var names = new List<string>();
        string? url = "/api/projects?limit=10";
        var pages = 0;

        while (url is not null)
        {
            var page = (await (await _client.GetAsync(url)).Content.ReadFromJsonAsync<JsonElement>());

            Assert.Equal(25, page.GetProperty("total").GetInt32());

            foreach (var item in page.GetProperty("items").EnumerateArray())
                names.Add(item.GetProperty("name").GetString()!);

            var next = page.GetProperty("nextLink");
            url = next.ValueKind == JsonValueKind.Null ? null : next.GetString();

            pages++;

            Assert.True(pages <= 3, "забагато сторінок");
        }

        Assert.Equal(3, pages);
        Assert.Equal(25, names.Count);

        Assert.Equal(25, names.Distinct().Count());

        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    //перевіряє отримання потрібної частини списку за параметрами skip і limit
    [Fact]
    public async Task SkipAndLimit_SelectCorrectSlice()
    {
        Seed(25);

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/projects?skip=20&limit=10");

        Assert.Equal(5, page.GetProperty("items").GetArrayLength());
        Assert.Equal(20, page.GetProperty("skip").GetInt32());

        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextLink").ValueKind);
    }

    //перевіряє використання стандартного значення limit = 20
    [Fact]
    public async Task DefaultLimit_Is20()
    {
        Seed(25);

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/projects");

        Assert.Equal(20, page.GetProperty("items").GetArrayLength());
        Assert.Equal(20, page.GetProperty("limit").GetInt32());

        Assert.NotEqual(JsonValueKind.Null, page.GetProperty("nextLink").ValueKind);
    }

    // перевіряє відхилення некоректних параметрів пагінації
    [Theory]
    [InlineData("skip=-1")]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    public async Task InvalidPaging_Returns400(string query)
    {
        var r = await _client.GetAsync("/api/projects?" + query);

        // для некоректних параметрів API має повертати Bad Request (400)
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}