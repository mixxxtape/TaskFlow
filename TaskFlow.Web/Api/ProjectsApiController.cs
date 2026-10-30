using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Api;

[ApiController]
[Route("api/projects")]
public class ProjectsApiController : ControllerBase
{
    private static readonly Expression<Func<Project, ProjectDto>> ToDto =
        p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt, p.Tasks.Count);

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public ProjectsApiController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectDto>>> List(int skip = 0, int limit = Paging.DefaultLimit)
    {
        if (Paging.Validate(skip, limit) is { } error) return Problem(detail: error, statusCode: 400);
        var query = _db.Projects.OrderBy(p => p.Id).Select(ToDto);
        return await Paging.ToPagedAsync(query, skip, limit, Request);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> Get(int id)
    {
        var dto = await _db.Projects.Where(p => p.Id == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound() : dto;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(ProjectInput input)
    {
        var project = new Project { Name = input.Name.Trim(), Description = input.Description, CreatedAt = DateTime.UtcNow };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        var dto = new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, 0);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectDto>> Update(int id, ProjectInput input)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        project.Name = input.Name.Trim();
        project.Description = input.Description;
        await _db.SaveChangesAsync();
        return await _db.Projects.Where(p => p.Id == id).Select(ToDto).SingleAsync();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _db.Projects.Include(p => p.Tasks).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        var files = project.Tasks.Select(t => t.AttachmentUrl).ToList();
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        foreach (var url in files) _storage.Delete(url);
        return NoContent();
    }
}
