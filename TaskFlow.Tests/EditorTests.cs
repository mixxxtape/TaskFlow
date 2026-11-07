using System.Net;

namespace TaskFlow.Tests;

public class EditorTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EditorTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    //перевірка, що сторінка редактора відкривається і містить контейнер, у якому будуються блоки
    [Fact]
    public async Task EditorPage_ContainsEditorContainer()
    {
        var response = await _client.GetAsync("/Editor");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"md-editor\"", html);
        Assert.Contains("md-editor.js", html);
    }
}