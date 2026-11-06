using System.Net;

namespace TaskFlow.Tests;

public class GameTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GameTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    //перевірка, що сторінка гри відкривається і містить canvas, на якому малюється гра
    [Fact]
    public async Task GamePage_ContainsCanvas()
    {
        var response = await _client.GetAsync("/Game");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<canvas id=\"game\"", html);
    }
}