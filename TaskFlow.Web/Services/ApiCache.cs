using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace TaskFlow.Web.Services;

public class ApiCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private CancellationTokenSource _cts = new();

    public ApiCache(IMemoryCache cache) => _cache = cache;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? cached)) return cached!;

        var token = _cts.Token; 
        var value = await factory();

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(Lifetime)
            .AddExpirationToken(new CancellationChangeToken(token));
        _cache.Set(key, value, options);
        return value;
    }

    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}