using Microsoft.AspNetCore.Hosting;

namespace TaskFlow.Tests;

public class AnalyticsTests
{
    private static async Task<string> GetHomeHtml(string measurementId)
    {
        using var factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseSetting("Analytics:GoogleMeasurementId", measurementId));
        var client = factory.CreateClient();
        return await client.GetStringAsync("/Projects");
    }

    //перевірка, що коли вказано Measurement ID, на сторінці з'являється скрипт Google Analytics
    [Fact]
    public async Task Page_WithMeasurementId_ContainsGoogleAnalytics()
    {
        var html = await GetHomeHtml("G-TEST12345");

        Assert.Contains("googletagmanager.com/gtag/js?id=G-TEST12345", html);
        Assert.Contains("gtag('config', 'G-TEST12345')", html);
    }

    //перевірка, що без Measurement ID скрипт аналітики не підключається
    [Fact]
    public async Task Page_WithoutMeasurementId_HasNoAnalytics()
    {
        var html = await GetHomeHtml("");

        Assert.DoesNotContain("googletagmanager.com", html);
    }
}