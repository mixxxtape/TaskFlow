namespace TaskFlow.Web.Services;

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public string? ChatId { get; set; }
}

public interface INotifier
{
    Task NotifyAsync(string text);
}

public class TelegramNotifier : INotifier
{
    private readonly HttpClient _http;
    private readonly TelegramOptions _options;

    public TelegramNotifier(HttpClient http, TelegramOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task NotifyAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.ChatId))
            return;

        try
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            await _http.PostAsJsonAsync(url, new { chat_id = _options.ChatId, text });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }
    }
}