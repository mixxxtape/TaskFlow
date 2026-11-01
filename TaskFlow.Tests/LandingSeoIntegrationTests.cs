using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

public class LandingSeoIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LandingSeoIntegrationTests(CustomWebApplicationFactory f)
    {
        _factory = f;
        _client = f.CreateClient();
    }

    //створення тестового проєкту з командою та двома завданнями в тестовій базі даних
    private async Task<int> SeedProject(string name = "Landing Test", string? email = "team@example.com", string? cover = "/uploads/cover.png")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Name = "Olga", Email = $"o{Guid.NewGuid():N}@x.com" };
        var project = new Project
        {
            Name = name,
            Description = "A short description of the project",
            ContactEmail = email,
            CoverImageUrl = cover,
            Tasks =
            {
                new TaskItem { Title = "Task Alpha", Status = ItemStatus.Done, Assignee = user },
                new TaskItem { Title = "Task Beta" }
            }
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }


    //отримання HTML сторінки лендінгу за ідентифікатором проєкту
    private Task<string> Html(int id) => _client.GetStringAsync($"/Projects/Details/{id}");

    //перевірка наявності SEO-метатегів, canonical URL та Open Graph і Twitter-тегів
    [Fact]
    public async Task Details_HasAllSeoTags()
    {
        var id = await SeedProject();

        var html = await Html(id);

        Assert.Contains("<html lang=\"uk\">", html);
        Assert.Contains("<title>Landing Test - TaskFlow</title>", html);
        Assert.Contains("<meta name=\"description\" content=\"A short description of the project\"", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"http://localhost/Projects/Details/{id}\"", html);
        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("property=\"og:image\" content=\"http://localhost/uploads/cover.png\"", html);
        Assert.Contains("name=\"twitter:card\" content=\"summary_large_image\"", html);
    }


    //перевірка, що сторінка має лише один H1 та містить основні семантичні секції
    [Fact]
    public async Task Details_HasExactlyOneH1_AndSemanticSections()
    {
        var html = await Html(await SeedProject());

        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.Contains("<header id=\"about\"", html);
        Assert.Contains("<section id=\"tasks\"", html);
        Assert.Contains("<section id=\"contact\"", html);
    }

    // превірка, що структуровані дані JSON-LD є валідним JSON та містять назву проєкту
    [Fact]
    public async Task Details_JsonLd_IsParsableAndDescribesProject()
    {
        var html = await Html(await SeedProject());

        var match = Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(match.Success);
        using var doc = JsonDocument.Parse(match.Groups[1].Value);
        Assert.Equal("Landing Test", doc.RootElement.GetProperty("name").GetString());
    }

    //перевірка, що кожне зображення на сторінці має альтернативний текст alt
    [Fact]
    public async Task Details_EveryImageHasAltText()
    {
        var html = await Html(await SeedProject());

        Assert.Contains("<img", html);
        Assert.DoesNotMatch(@"<img\b(?![^>]*\balt="")[^>]*>", html);
    }

    // перевірка, що нижня панель контактів закріплена на екрані та містить посилання на email
    [Fact]
    public async Task Details_ShowsFixedContactBar_WithMailtoLink()
    {
        var html = await Html(await SeedProject(email: "team@example.com"));

        Assert.Contains("fixed-bottom", html);
        Assert.Contains("href=\"mailto:team@example.com\"", html);
    }

    //перевірка, що без контактного email відображається пропозиція додати його
    [Fact]
    public async Task Details_WithoutContact_OffersToAddOne()
    {
        var html = await Html(await SeedProject(name: "No Contact", email: null));

        Assert.Contains("fixed-bottom", html);
        Assert.Contains("Додати контакт", html);
        Assert.DoesNotContain("mailto:", html);
    }

    //перевірка, що без обкладинки не генерується Open Graph зображення та використовується звичайна Twitter-картка
    [Fact]
    public async Task Details_WithoutCover_HasNoOgImage()
    {
        var html = await Html(await SeedProject(name: "No Cover", cover: null));

        Assert.DoesNotContain("og:image", html);
        Assert.Contains("name=\"twitter:card\" content=\"summary\"", html);
    }


    //перевірка, що на лендінгу відображаються завдання та учасники команди
    [Fact]
    public async Task Details_ShowsTasksAndTeam()
    {
        var html = await Html(await SeedProject());

        Assert.Contains("Task Alpha", html);
        Assert.Contains("Task Beta", html);
        Assert.Contains("Olga", html);
    }


    //перевірка, що sitemap.xml повертає валідну відповідь XML та містить сторінку створеного проєкту
    [Fact]
    public async Task Sitemap_IsXml_AndListsProject()
    {
        var id = await SeedProject();

        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains($"http://localhost/Projects/Details/{id}", body);
    }

    //перевірка, що robots.txt повертається успішно та містить посилання на sitemap.xml
    [Fact]
    public async Task Robots_ReferencesSitemap()
    {
        var response = await _client.GetAsync("/robots.txt");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sitemap: http://localhost/sitemap.xml", body);
    }

    //перевірка, що інші сторінки сайту також отримують стандартний meta description
    [Fact]
    public async Task OtherPages_GetDefaultMetaDescription()
    {
        var html = await _client.GetStringAsync("/Projects");
        Assert.Contains("<meta name=\"description\"", html);
    }

    //перевірка, що сторінка статистики містить три діаграми та посилання на JSON-дані
    [Fact]
    public async Task StatsPage_RendersChartCanvases()
    {
        var html = await _client.GetStringAsync("/Stats");

        Assert.Contains("id=\"chartStatus\"", html);
        Assert.Contains("id=\"chartProject\"", html);
        Assert.Contains("id=\"chartAssignee\"", html);
        Assert.Contains("data-url=\"/Stats/Data\"", html);
    }

    //перевірка, що endpoint статистики повертає JSON з даними за статусами, проєктами та загальною кількістю завдань
    [Fact]
    public async Task StatsData_ReturnsJsonArrays()
    {
        await SeedProject();

        var response = await _client.GetAsync("/Stats/Data");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, json.GetProperty("byStatus").GetArrayLength());
        Assert.True(json.GetProperty("total").GetInt32() >= 2);
        Assert.True(json.GetProperty("byProject").GetArrayLength() >= 1);
    }
}
