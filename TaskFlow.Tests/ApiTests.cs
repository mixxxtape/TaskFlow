using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.Tests;

//тести для перевірки роботи REST API
public class ApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    //створення HTTP-клієнта для надсилання запитів до тестового API
    public ApiTests(CustomWebApplicationFactory f) => _client = f.CreateClient();

    //метод для читання відповіді сервера у форматі JSON
    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>())!;

    //створення тестового проєкту та отримання його ID
    private async Task<int> CreateProject(string name = "P")
    {
        var r = await _client.PostAsJsonAsync("/api/projects", new { name });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await Json(r)).GetProperty("id").GetInt32();
    }


    //перевірка, що після створення проєкту повертається Created, посилання Location і його можна отримати через GET-запит
    [Fact]
    public async Task Project_Create_Returns201_WithLocation_AndCanBeFetched()
    {
        var r = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Alpha", description = "d" });

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.NotNull(r.Headers.Location);

        var get = await _client.GetAsync(r.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("Alpha", (await Json(get)).GetProperty("name").GetString());
    }


    //перевірка, що проєкт із порожньою назвою не створюється
    [Fact]
    public async Task Project_Create_EmptyName_Returns400()
    {
        var r = await _client.PostAsJsonAsync(
            "/api/projects", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }


    //перевірка, що при спробі отримати неіснуючий проєкт повертається NotFound
    [Fact]
    public async Task Project_Get_Unknown_Returns404()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/projects/999999")).StatusCode);
    }


    //перевірка, що після редагування назва проєкту змінюється
    [Fact]
    public async Task Project_Update_Returns200_AndChangesData()
    {
        var id = await CreateProject("Old");

        var put = await _client.PutAsJsonAsync(
            $"/api/projects/{id}", new { name = "New" });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("New", (await Json(put)).GetProperty("name").GetString());
    }


    //перевірка, що при спробі редагувати неіснуючий проєкт повертається NotFound
    [Fact]
    public async Task Project_Update_Unknown_Returns404()
    {
        var put = await _client.PutAsJsonAsync(
            "/api/projects/999999", new { name = "x" });

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }


    //перевірка, що після видалення проєкту його більше не можна отримати або видалити повторно
    [Fact]
    public async Task Project_Delete_Returns204_ThenGet404()
    {
        var id = await CreateProject();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/projects/{id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/projects/{id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.DeleteAsync($"/api/projects/{id}")).StatusCode);
    }


    //перевірка, що завдання створюється з міткою та правильним статусом
    [Fact]
    public async Task Task_Create_WithLabels_Returns201_AndStatusAsString()
    {
        var projectId = await CreateProject();

        var label = await Json(await _client.PostAsJsonAsync(
            "/api/labels",
            new { name = "bug", color = "#ff0000" }));

        var labelId = label.GetProperty("id").GetInt32();

        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Fix",
            projectId,
            status = "InProgress",
            labelIds = new[] { labelId }
        });

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        var body = await Json(r);

        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal(labelId, body.GetProperty("labelIds")[0].GetInt32());
    }


    //перевірка, що завдання не створюється з ID неіснуючого проєкту
    [Fact]
    public async Task Task_Create_UnknownProject_Returns400()
    {
        var r = await _client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "Orphan", projectId = 999999 });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }


    //перевірка, що завдання з порожньою назвою не створюється
    [Fact]
    public async Task Task_Create_MissingTitle_Returns400()
    {
        var projectId = await CreateProject();

        var r = await _client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "", projectId });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }


    //перевірка, що завдання можна створити, відредагувати та видалити, а після видалення воно більше не доступне
    [Fact]
    public async Task Task_Update_And_Delete_Work()
    {
        var projectId = await CreateProject();

        var created = await Json(await _client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "T", projectId }));

        var id = created.GetProperty("id").GetInt32();

        var put = await _client.PutAsJsonAsync(
            $"/api/tasks/{id}",
            new { title = "T2", projectId, status = "Done" });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Done", (await Json(put)).GetProperty("status").GetString());

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/tasks/{id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/tasks/{id}")).StatusCode);
    }


    //перевірка, що файл можна завантажити до завдання і його посилання зберігається
    [Fact]
    public async Task Task_UploadAttachment_StoresUrl()
    {
        var projectId = await CreateProject();

        var created = await Json(await _client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "T", projectId }));

        var id = created.GetProperty("id").GetInt32();

        using var form = new MultipartFormDataContent();

        form.Add(
            new ByteArrayContent(new byte[] { 1, 2, 3 }),
            "file",
            "spec.pdf");

        var r = await _client.PostAsync(
            $"/api/tasks/{id}/attachment", form);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var url = (await Json(r)).GetProperty("attachmentUrl").GetString();

        Assert.StartsWith("/uploads/", url);
        Assert.False(string.IsNullOrEmpty(url));
    }


    //перевірка, що коментар додається до завдання та повертається у списку разом з ім'ям автора
    [Fact]
    public async Task Task_Comments_AddAndList()
    {
        var projectId = await CreateProject();

        var task = await Json(await _client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "T", projectId }));

        var taskId = task.GetProperty("id").GetInt32();

        var user = await Json(await _client.PostAsJsonAsync(
            "/api/users",
            new { name = "Ann", email = $"ann{Guid.NewGuid():N}@x.com" }));

        var userId = user.GetProperty("id").GetInt32();

        var add = await _client.PostAsJsonAsync(
            $"/api/tasks/{taskId}/comments",
            new { text = "Hi", authorId = userId });

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var list = await Json(await _client.GetAsync(
            $"/api/tasks/{taskId}/comments"));

        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Ann", list[0].GetProperty("authorName").GetString());
    }


    //перевірка, що користувача з email, який вже є в базі, не можна створити повторно
    [Fact]
    public async Task User_Create_DuplicateEmail_Returns400()
    {
        var email = $"dup{Guid.NewGuid():N}@x.com";

        Assert.Equal(
            HttpStatusCode.Created,
            (await _client.PostAsJsonAsync(
                "/api/users",
                new { name = "A", email })).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await _client.PostAsJsonAsync(
                "/api/users",
                new { name = "B", email })).StatusCode);
    }


    //перевірка, що мітка з некоректним кольором не створюється
    [Fact]
    public async Task Label_InvalidColor_Returns400()
    {
        var r = await _client.PostAsJsonAsync(
            "/api/labels",
            new { name = "x", color = "red" });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }


    //перевірка, що документація Swagger генерується та містить маршрут для роботи з проєктами
    [Fact]
    public async Task Swagger_Document_IsGenerated()
    {
        var r = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var doc = await Json(r);

        Assert.True(
            doc.GetProperty("paths").TryGetProperty("/api/projects", out _));
    }
}