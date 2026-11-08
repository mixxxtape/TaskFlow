using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Web.Services;

public record LiveEvent(long Id, string Message, long SentAtMs);

public class LiveState
{
    private long _lastId;
    private volatile LiveEvent? _last;

    public LiveEvent? Last => _last;

    public LiveEvent Next(string message)
    {
        var e = new LiveEvent(Interlocked.Increment(ref _lastId), message,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _last = e;
        return e;
    }
}

public interface ILiveBroadcaster
{
    Task PublishAsync(string message);
}

public class LiveBroadcaster : ILiveBroadcaster
{
    private readonly IHubContext<LiveHub> _hub;
    private readonly LiveState _state;

    public LiveBroadcaster(IHubContext<LiveHub> hub, LiveState state)
    {
        _hub = hub;
        _state = state;
    }

    public async Task PublishAsync(string message)
    {
        var e = _state.Next(message);
        await _hub.Clients.All.SendAsync("LiveEvent", e);
    }
}

public class LiveHub : Hub
{
    public Task SendDocument(string json) => Clients.Others.SendAsync("DocumentChanged", json);
}