using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Web.Api;


//допоміжний клас для реалізації пагінації в API.
public static class Paging
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static string? Validate(int skip, int limit)
    {
        if (skip < 0) return "skip must be >= 0";
        if (limit < 1 || limit > MaxLimit) return $"limit must be between 1 and {MaxLimit}";
        return null;
    }

    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> query, int skip, int limit, HttpRequest request)
    {
        var total = await query.CountAsync();
        var items = await query.Skip(skip).Take(limit).ToListAsync();

        string? next = null;
        if (skip + limit < total)
        {
            var qs = request.Query
                .Where(kv => kv.Key != "skip" && kv.Key != "limit")
                .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
            qs["skip"] = (skip + limit).ToString();
            qs["limit"] = limit.ToString();
            next = QueryHelpers.AddQueryString(
                $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}", qs);
        }
        return new PagedResult<T>(items, total, skip, limit, next);
    }
}
