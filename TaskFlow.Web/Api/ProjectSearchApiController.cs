using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;

namespace TaskFlow.Web.Api;

public record SelectOption(int Id, string Text);
public record SelectResult(List<SelectOption> Results);

[ApiController]
[Route("api/projects/search")]
public class ProjectSearchApiController : ControllerBase
{
    public const int MinLength = 3;
    public const int MaxResults = 20;

    private readonly AppDbContext _db;
    public ProjectSearchApiController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<SelectResult>> Search(string? term)
    {
        term = term?.Trim().ToLower();
        if (term is null || term.Length < MinLength)
            return new SelectResult(new List<SelectOption>());

        var items = await _db.Projects
            .Where(p => p.Name.ToLower().Contains(term))
            .OrderBy(p => p.Name)
            .Take(MaxResults)
            .Select(p => new SelectOption(p.Id, p.Name))
            .ToListAsync();

        return new SelectResult(items);
    }
}