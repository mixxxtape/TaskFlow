using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskFlow.Tests;

public class FirstTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public FirstTest(WebApplicationFactory<Program> f) => _factory = f;

    [Fact]
    public async Task HomePage_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}