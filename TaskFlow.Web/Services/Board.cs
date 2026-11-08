using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Web.Services;

public record Stroke(double X0, double Y0, double X1, double Y1, string Color, int Size)
{
    private static readonly Regex ColorPattern = new("^#[0-9a-fA-F]{6}$");

    public static Stroke Clean(Stroke s) => new(
        ClampCoord(s.X0), ClampCoord(s.Y0), ClampCoord(s.X1), ClampCoord(s.Y1),
        s.Color is not null && ColorPattern.IsMatch(s.Color) ? s.Color : "#000000",
        Math.Clamp(s.Size, 1, 30));

    private static double ClampCoord(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 2000);
}

//спільний стан дошки
public class BoardState
{
    public const int MaxStrokes = 20000;

    private readonly List<Stroke> _strokes = new();
    private readonly object _lock = new();
    private int _users;

    public void Add(Stroke stroke)
    {
        lock (_lock)
        {
            if (_strokes.Count >= MaxStrokes) _strokes.RemoveRange(0, 1000);
            _strokes.Add(stroke);
        }
    }

    public List<Stroke> Snapshot()
    {
        lock (_lock) return _strokes.ToList();
    }

    public void Clear()
    {
        lock (_lock) _strokes.Clear();
    }

    public int UserJoined() => Interlocked.Increment(ref _users);
    public int UserLeft() => Interlocked.Decrement(ref _users);
}

public class BoardHub : Hub
{
    private readonly BoardState _state;

    public BoardHub(BoardState state) => _state = state;

    public override async Task OnConnectedAsync()
    {
        var count = _state.UserJoined();
        await Clients.Caller.SendAsync("History", _state.Snapshot());
        await Clients.All.SendAsync("Users", count);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var count = _state.UserLeft();
        await Clients.All.SendAsync("Users", count);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task Draw(Stroke stroke)
    {
        var clean = Stroke.Clean(stroke);
        _state.Add(clean);
        await Clients.Others.SendAsync("Stroke", clean);
    }

    public async Task Clear()
    {
        _state.Clear();
        await Clients.Others.SendAsync("Cleared");
    }
}