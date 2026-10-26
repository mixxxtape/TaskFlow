using System.Net;

namespace TaskFlow.Tests;

public class SmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public SmokeTests(CustomWebApplicationFactory f) => _factory = f;

    [Theory]
    [InlineData("/")]
    [InlineData("/Projects")]
    [InlineData("/Tasks")]
    [InlineData("/Tasks/Create")]
    [InlineData("/Projects/Create")]
    [InlineData("/Users")]
    [InlineData("/Labels")]
    public async Task Pages_Return200(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Projects/Details/999")]
    [InlineData("/Tasks/Details/999")]
    [InlineData("/Tasks/Edit/999")]
    [InlineData("/Users/Edit/999")]
    public async Task UnknownId_Returns404(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
