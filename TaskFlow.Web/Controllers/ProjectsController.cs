using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

public class ProjectsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public ProjectsController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _db.Projects.Include(p => p.Tasks)
            .OrderByDescending(p => p.Id).ToListAsync();
        return View(projects);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .FirstOrDefaultAsync(p => p.Id == id);
        return project is null ? NotFound() : View(project);
    }

    public IActionResult Create() => View(new Project());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description")] Project project)
    {
        if (!ModelState.IsValid) return View(project);
        project.CreatedAt = DateTime.UtcNow;
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects.FindAsync(id);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Project input)
    {
        if (id != input.Id) return NotFound();
        if (!ModelState.IsValid) return View(input);

        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        project.Name = input.Name;
        project.Description = input.Description;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects.Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var project = await _db.Projects.Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is not null)
        {
            var files = project.Tasks.Select(t => t.AttachmentUrl).ToList();
            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
            foreach (var url in files) _storage.Delete(url);
        }
        return RedirectToAction(nameof(Index));
    }
}
